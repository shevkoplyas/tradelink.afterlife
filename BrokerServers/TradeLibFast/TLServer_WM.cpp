// TLServer_WM.cpp : implementation file
//

#include "stdafx.h"
#include "TLServer_WM.h"
#include "TradeLink.h"
#include "Util.h"
#include <fstream>
using namespace std;


namespace TradeLibFast
{

	const char* VERFILE = "\\tradelink\\VERSION.txt";
	TLServer_WM::TLServer_WM(void)
	{
		PROGRAM = CString("BROKER");
		MajorVer = 0.3;
		MinorVer = 1;
		TLDEBUG_LEVEL = 3;	// dimon: set to any negative value to disable debug. 0 = normal, 1 = more verbose...  3=max verbosity level
		ENABLED = false;	// dimon: this "flag" will be used once in Start().. kinda ugly solution against multiple start() calls?
		LOGENABLED = true;
		debugbuffer = CString("");

		NEWLINE = "\r\n";
		//NEWLINE = "\n";

		same_sec_timestamp_counter = 0;
		previously_reported_TLtime = 0;

		/*
		"we no longer read version number from: C:\Program Files (x86)\TradeLink\VERSION.txt"
		std::ifstream file;
		TCHAR path[MAX_PATH];
		SHGetFolderPath(NULL,CSIDL_PROGRAM_FILES,NULL,0,path);
		CString ver(path);
		ver.Append(VERFILE);
		file.open(ver.GetBuffer());
		if (file.is_open())
		{
		char data[8];
		file.getline(data,8);
		MinorVer = atoi(data);
		file.close();
		}
		*/

		// thread setup stuff

		for (uint i = 0; i<MAXTICKS; i++)
		{
			TLTick k;
			_tickcache.push_back(k);
		}
		CString debug_message;
		debug_message.Format("Note: your _tickcache size (set by MAXTICKS): %d", MAXTICKS);
		D(debug_message);

		_tickflip = false;
		_readticks = 0;
		_writeticks = 0;
		_go = true;
		_startthread = false;
	}

	TLServer_WM::~TLServer_WM()
	{
		// ensure threads are marked to stop
		_go = false;

		// wait moment for them to stop  
		Sleep(100); // dimon: this is ugly

		// clear tick and imbalance cache
		_tickcache.clear();

		// signal threads to stop
		//SetEvent(_tickswaiting);
		debugbuffer = "";

	}

	CString TLServer_WM::Version()
	{
		CString ver;
		ver.Format("%.1f.%i",MajorVer,MinorVer);
		D("TLServer_WM::Version(): " + ver);
		return ver;
	}


	BEGIN_MESSAGE_MAP(TLServer_WM, CWnd)
		ON_WM_COPYDATA()
	END_MESSAGE_MAP()


	bool TLServer_WM::needStock(CString stock)
	{
		D("TLServer_WM::needStock " + stock);
		int idx = FindSym(stock);
		if (idx==-1) return false;
		bool needed = symclientidx[idx].size()!=0;
		return needed;
	}

	int TLServer_WM::FindClient(CString cwind)
	{
		D("TLServer_WM::FindClient(CString cwind)");
		size_t len = client.size();
		for (size_t i = 0; i<len; i++) if (client[i]==cwind) return (int)i;
		return -1;
	}

	CString SerializeIntVec(std::vector<int> input)
	{
		std::vector<CString> tmp;
		for (size_t i = 0; i<input.size(); i++)
		{
			CString t; // setup tmp string
			t.Format("%i",input[i]); // convert integer into tmp string
			tmp.push_back(t); // push converted string onto vector
		}
		// join vector and return serialized structure
		return gjoin(tmp,",");
	}

	int TLServer_WM::FindSym(CString sym)
	{
		for (uint i = 0; i<symindex.size(); i++)
		{
			if (symindex[i]==sym) return i;
		}
		return -1;
	}

	void TLServer_WM::IndexBaskets()
	{
		// this function builds an index of all client subscribed to each symbol

		// go through every client's symbols
		for (uint c = 0; c<stocks.size(); c++)
		{
			// get current client
			clientstocklist hisstocks = stocks[c];
			// go through every symbol
			for (uint i = 0; i<hisstocks.size(); i++)
			{
				// get client's next symbol
				CString sym = hisstocks[i];
				// see if a client has subscribed to it already
				int idx = FindSym(sym);
				// if this client is first subscriber, add the symbol
				if (idx==-1) 
				{
					// never seen this symbol so add it
					symindex.push_back(sym);
					// keep track of the index
					idx = (int)symindex.size()-1;
					// create an index to hold all clients watching this symbol
					clientindex tmp;
					// add our client to the index
					tmp.push_back(c);
					// save the index at the same offset as the symbol index
					symclientidx.push_back(tmp);
				}
				else // somebody has already subscribed to this symbol before
				{
					// now that we have the symbol indexed,
					// lets go through every client and make sure
					// 1) client is subscribing to this symbol
					// 2) if he's subscribed, only subscribed once

					// we'll build a new index to replace current one
					clientindex newidx;
					// go through every client
					for (uint cid = 0; cid<stocks.size(); cid++)
					{
						// if client still wants symbol, add him to index
						if (ClientHasSymbol(cid,sym))
							newidx.push_back(cid);
					}
					// save index for this symbol and continue to next one
					symclientidx[idx] = newidx;
				}
			}
		}
	}

	bool TLServer_WM::ClientHasSymbol(int clientid, CString sym)
	{
		for (uint i = 0; i<stocks[clientid].size(); i++)
			if (stocks[clientid][i]==sym)
				return true;
		return false;
	}

	// TLServer_WM message handlers


	BOOL TLServer_WM::OnCopyData(CWnd* pWnd, COPYDATASTRUCT* pCopyDataStruct)
	{
		CString msg = (LPCTSTR)(pCopyDataStruct->lpData);
		int type = (int)pCopyDataStruct->dwData;
		switch (type)
		{
		case ORDERCANCELREQUEST :
			{
				D4("ORDERCANCELREQUEST");
				const char * ch = msg.GetBuffer();
				int64 id = _atoi64(ch);
				return CancelRequest(id);
			}
		case ACCOUNTREQUEST :
			D4("ACCOUNTREQUEST");
			return AccountResponse(msg);
		case CLEARCLIENT :
			D4("CLEARCLIENT");
			return ClearClient(msg);
		case CLEARSTOCKS :
			D4("CLEARSTOCKS");
			return ClearStocks(msg);
		case REGISTERSTOCK :
			{
				D4("REGISTERSTOCK");
				vector<CString> rec;
				gsplit(msg,CString("+"),rec);
				CString client = rec[0];
				vector<CString> hisstocks;
				// make sure client sent a basket, otherwise clear the basket
				if (rec.size()!=2) return ClearStocks(client);
				// get the basket
				gsplit(rec[1],CString(","),hisstocks);
				// make sure we have the client
				unsigned int cid = FindClient(client); 
				if (cid==-1) return CLIENTNOTREGISTERED; //client not registered
				// save the basket
				stocks[cid] = hisstocks; 
				// index his basket
				IndexBaskets();
				D(CString(_T("Client ")+client+_T(" registered: ")+gjoin(hisstocks,",")));
				HeartBeat(client);
				return RegisterStocks(client);
			}
		case POSITIONREQUEST :
			{
				D4("REGISTERSTOCK");
				vector<CString> r;
				gsplit(msg,CString("+"),r);
				if (r.size()!=2) return UNKNOWN_MESSAGE;
				return PositionResponse(r[1],r[0]);
			}
		case REGISTERCLIENT :
			D4("REGISTERCLIENT");
			return RegisterClient(msg);
		case HEARTBEATREQUEST :
			D4("HEARTBEATREQUEST");
			return HeartBeat(msg);
		case BROKERNAME :
			D4("BROKERNAME");
			return BrokerName();
		case SENDORDER :
			D4("SENDORDER");
			return SendOrder(TLOrder::Deserialize(msg));
		case FEATUREREQUEST:
			{
				D4("FEATUREREQUEST");
				// get features supported by child class
				std::vector<int> stub = GetFeatures();
				// append basic feature we provide as parent
				stub.push_back(REGISTERCLIENT);
				stub.push_back(HEARTBEATREQUEST);
				stub.push_back(CLEARSTOCKS);
				stub.push_back(CLEARCLIENT);
				stub.push_back(VERSION);
				// send entire feature set back to client
				D3("FEATURERESPONSE" + SerializeIntVec(stub));
				TLSend(FEATURERESPONSE,SerializeIntVec(stub),msg);
				return OK;
			}
		case VERSION :
			D4("VERSION");

			return MinorVer;
		case DOMREQUEST :
			{
				D4("DOMREQUEST");
				vector<CString> rec;
				gsplit(msg,CString("+"),rec);
				CString client = rec[0];
				// make sure we have the client
				unsigned int cid = FindClient(client); 
				if (cid==-1) return CLIENTNOTREGISTERED; //client not registered
				D(CString(_T("Client ")+client+_T(" registered: ")));
				HeartBeat(client);
				return DOMRequest(atoi(rec[1]));
			}
		default: // unknown messages
			{
				D4("default: unknown message from client");
				int um = UnknownMessage(type,msg);
				// issue #141
				CString data;
				data.Format("%i",um);
				for (uint i = 0; i<client.size(); i++)
				{
					D3("WARNING! response to unknown message back to client");
					TLSend(type,data,i);
				}
				// this will go away soon
				return um;

			}
		}

		return FEATURE_NOT_IMPLEMENTED;
	}


	int TLServer_WM::RegisterClient(CString  clientname)
	{
		// make sure client is unique
		if (FindClient(clientname)!=-1) return OK;
		// save client
		client.push_back(clientname);
		// get handle to client
		HWND dest = FindWindowA(NULL,(LPCSTR)(LPCTSTR)clientname)->GetSafeHwnd();
		// save client handle
		hims.push_back(dest);
		// get time
		time_t now;
		time(&now);
		// save time as last heartbeat
		heart.push_back(now); // save heartbeat at client index
		// save empty list of symbols
		clientstocklist my = clientstocklist(0);
		stocks.push_back(my);
		// notify users
		D(CString(_T("Client ")+clientname+_T(" connected.")));
		return OK;
	}

	int TLServer_WM::UnknownMessage(int MessageType, CString msg)
	{
		return UNKNOWN_MESSAGE;
	}

	int TLServer_WM::HeartBeat(CString clientname)
	{
		if (this->TLDEBUG_LEVEL >= 3){ // extra check of debug level since we have extra formatting work here and dont wanna slowdown production
			CString debug_message;
			debug_message.Format("TLServer_WM::HeartBeat: clientname='%s'", clientname);
			D3(debug_message);
		}

		int cid = FindClient(clientname);
		if (cid==-1) return -1;
		time_t now;
		time(&now);
		time_t then = heart[cid];
		double dif = difftime(now,then);
		heart[cid] = now;
		return (int)dif;
	}

	int TLServer_WM::RegisterStocks(CString clientname) 
	{ 
		return OK; 
	}
	int TLServer_WM::DOMRequest(int depth) { return OK; }
	std::vector<int> TLServer_WM::GetFeatures() { std::vector<int> blank; return blank; } 

	int TLServer_WM::AccountResponse(CString clientname)
	{
		return FEATURE_NOT_IMPLEMENTED;
	}

	int TLServer_WM::PositionResponse(CString account, CString clientname)
	{
		return FEATURE_NOT_IMPLEMENTED;
	}

	int TLServer_WM::BrokerName(void)
	{
		return TradeLink;
	}

	int TLServer_WM::SendOrder(TLOrder order)
	{
		return FEATURE_NOT_IMPLEMENTED;
	}

	int TLServer_WM::ClearClient(CString clientname)
	{
		int cid = FindClient(clientname);
		if (cid==-1) return CLIENTNOTREGISTERED;
		client[cid] = "";
		stocks[cid] = clientstocklist(0);
		heart[cid] = NULL;
		hims[cid] = NULL;
		D(CString(_T("Client ")+clientname+_T(" disconnected.")));
		return OK;
	}
	int TLServer_WM::ClearStocks(CString clientname)
	{
		int cid = FindClient(clientname);
		if (cid==-1) return CLIENTNOTREGISTERED;
		stocks[cid] = clientstocklist(0);
		IndexBaskets();
		HeartBeat(clientname);
		D(CString(_T("Cleared stocks for ")+clientname));
		return OK;
	}

	int TLServer_WM::get_same_sec_timestamp_counter( int TLtime_now)
	{
		// are we still inside same as "prev" second?
		if (previously_reported_TLtime != TLtime_now)
		{
			// no.. update "prev" value
			previously_reported_TLtime = TLtime_now;
			// reset counter
			same_sec_timestamp_counter = 0;
			return same_sec_timestamp_counter;
		}
		else
		{
			// yes, same. Increase counter
			same_sec_timestamp_counter++;
			return same_sec_timestamp_counter;
		}
	}

	void TLServer_WM::D(const CString & message)
	{

		if (this->TLDEBUG_LEVEL >= 0)
		{
			CString line;
			vector<int> now;
			TLTimeNow(now);
			int tltime_counter = get_same_sec_timestamp_counter(now[TLtime]);
			line.Format("%06i.%02i %s", now[TLtime], tltime_counter, message);
			debugbuffer.Append(line);
			CString prefix("commn:");
			if (LOGENABLED && line.GetLength() > 0)	// dimon: and skip empty lines in a log file
			{
				// write it
				log << prefix << line << endl;
				// ensure log is written now
				log.flush();
			}
			__raise this->GotDebug( line, now[TLtime],tltime_counter);
		}
	}

	void TLServer_WM::D1(const CString & message)
	{

		if (this->TLDEBUG_LEVEL >= 1)
		{
			CString line;
			vector<int> now;//(2); // will hold 2 integers: date, time
			TLTimeNow(now);
			int tltime_counter = get_same_sec_timestamp_counter(now[TLtime]);
			line.Format("%06i.%02i %s", now[TLtime], tltime_counter, message);
			debugbuffer.Append(line);
			CString prefix("IB->S:");
			if (LOGENABLED && line.GetLength() > 0)	// dimon: and skip empty lines in a log file
			{
				// write it
				log << prefix << line << endl;
				// ensure log is written now
				log.flush();
			}
			__raise this->GotDebug1(line, now[TLtime],tltime_counter);
		}
	}

	void TLServer_WM::D2(const CString & message)
	{

		if (this->TLDEBUG_LEVEL >= 2)
		{
			CString line;
			vector<int> now;
			TLTimeNow(now);
			int tltime_counter = get_same_sec_timestamp_counter(now[TLtime]);
			line.Format("%06i.%02i %s", now[TLtime], tltime_counter, message);
			debugbuffer.Append(line);
			CString prefix("IB<-S:");
			if (LOGENABLED && line.GetLength() > 0)	// dimon: and skip empty lines in a log file
			{
				// write it
				log << prefix << line << endl;
				// ensure log is written now
				log.flush();
			}
			__raise this->GotDebug2( line, now[TLtime],tltime_counter);
		}
	}

	void TLServer_WM::D3(const CString & message)
	{
		if (this->TLDEBUG_LEVEL >= 3)
		{
			CString line;
			vector<int> now;
			TLTimeNow(now);
			int tltime_counter = get_same_sec_timestamp_counter(now[TLtime]);
			line.Format("%06i.%02i %s", now[TLtime], tltime_counter, message);
			debugbuffer.Append(line);
			CString prefix("S->CL:");
			if (LOGENABLED && line.GetLength() > 0)	// dimon: and skip empty lines in a log file
			{
				// write it
				log << prefix << line << endl;
				// ensure log is written now
				log.flush();
			}
			__raise this->GotDebug3( line, now[TLtime],tltime_counter);
		}
	}

	void TLServer_WM::D4(const CString & message)
	{

		if (this->TLDEBUG_LEVEL >= 4)
		{
			CString line;
			vector<int> now;
			TLTimeNow(now);
			int tltime_counter = get_same_sec_timestamp_counter(now[TLtime]);
			line.Format("%06i.%02i %s", now[TLtime], tltime_counter, message);
			debugbuffer.Append(line);
			CString prefix("S<-CL:");
			if (LOGENABLED && line.GetLength() > 0)	// dimon: and skip empty lines in a log file
			{
				// write it
				log << prefix << line << endl;
				// ensure log is written now
				log.flush();
			}
			__raise this->GotDebug4( line, now[TLtime],tltime_counter);
		}
	}

	void TLServer_WM::SrvGotOrder(TLOrder order)
	{
		if (!order.isValid()) 
			return;
		for (size_t i = 0; i<client.size(); i++)
			if (client[i]!="")
			{
				D3("TLServer_WM::SrvGotOrder: ORDERNOTIFY" + order.Serialize());
				TLSend(ORDERNOTIFY,order.Serialize(),client[i]);
			}
	}

	void TLServer_WM::SrvGotFill(TLTrade trade)
	{
		if (!trade.isValid()) return;
		for (size_t i = 0; i<client.size(); i++)
			if (client[i]!="")
			{
				D3("TLServer_WM::SrvGotFill: EXECUTENOTIFY" + trade.Serialize());
				TLSend(EXECUTENOTIFY,trade.Serialize(),client[i]);
			}
	}

	void TLServer_WM::SrvGotTick(TLTick tick)
	{
		D1("SrvGotTick:"+tick.Serialize());
		// if tick has no symbol index, send it old way
		if (tick.symid<0)
		{

			for (uint i = 0; i<stocks.size(); i++)
				for (uint j = 0; j<stocks[i].size(); j++)
				{
					if (stocks[i][j]==tick.sym)
					{
						D3("TLServer_WM::SrvGotTick: TICKNOTIFY (a)" + tick.Serialize());
						TLSend(TICKNOTIFY,tick.Serialize(),i);
					}
				}
				return;
		}
		// otherwise get only clients by their index
		clientindex symclients = symclientidx[tick.symid];
		for (uint i = 0; i<symclients.size(); i++)
		{
			D3("TLServer_WM::SrvGotTick: TICKNOTIFY (b)" + tick.Serialize());
			TLSend(TICKNOTIFY,tick.Serialize(),symclients[i]);
		}
	}

	void TLServer_WM::SrvGotCancel(int64 orderid)
	{
		D1("SrvGotCancel");
		CString id;
		id.Format(_T("%I64d"),orderid);
		D1("SrvGotCancel"+id);
		for (size_t i = 0; i<client.size(); i++)
			if (client[i]!="")
			{
				D3("TLServer_WM::SrvGotCancel: ORDERCANCELRESPONSE");
				TLSend(ORDERCANCELRESPONSE,id,client[i]);
			}
	}

	int TLServer_WM::CancelRequest(int64 order)
	{
		D("warning: CancelRequest - FEATURE_NOT_IMPLEMENTED");
		return FEATURE_NOT_IMPLEMENTED;
	}

	UINT __cdecl DoReadTickThread(LPVOID param)
	{
		// we need a queue object
		TLServer_WM* tl = (TLServer_WM*)param;
		// ensure it's present
		if (tl==NULL)
		{
			return OK;
		}

		// process until quick req
		while (tl->_go)
		{
			// process ticks in queue
			while (tl->_go && (tl->_readticks < tl->_tickcache.size()))
			{
				// if we're done reading, quit trying
				if ((tl->_readticks>=tl->_writeticks) && !tl->_tickflip)
					break;
				// read next tick from cache
				TLTick k;
				k = tl->_tickcache[tl->_readticks++];
				// send it
				tl->SrvGotTick(k);
				// if we hit end of cache buffer, ring back around to start
				if (tl->_readticks>=tl->_tickcache.size())
				{
					tl->_readticks = 0;
					tl->_tickflip = false;
				}

				// this is from asyncresponse, but may not be same
				// functions because it doesn't appear to behave as nicely
				//ResetEvent(tl->_tickswaiting);
				//WaitForSingleObject(tl->_tickswaiting,INFINITE);
			}
			Sleep(100);
		}
		// mark thread as terminating
		tl->_startthread = false;
		// end thread
		return OK;
	}

	void TLServer_WM::SrvGotTickAsync(TLTick k)
	{
		D1("SrvGotTickAsync");
		// if thread is stopped don't restart it
		if (!_go) return;
		// add tick to queue and increment
		_tickcache[_writeticks++] = k;
		// implement ringbuffer on queue
		if (_writeticks>=_tickcache.size())
		{
			_writeticks = 0;
			_tickflip = true;
		}
		// ensure that we're reading from thread
		if (!_startthread)
		{
			AfxBeginThread(DoReadTickThread,this);
			_startthread = true;
		}
		else
		{
			// signal read thread that ticks are ready (adapted from asyncresponse)
			//SetEvent(_tickswaiting);
		}
	}



	bool TLServer_WM::HaveSubscriber(CString stock)
	{
		for (size_t i = 0; i<stocks.size(); i++) // go through each client
			for (size_t j = 0; j<stocks[i].size(); j++) // and each stock
				if (stocks[i][j].CompareNoCase(stock)==0) 
					return true;
		return false;
	}
	bool checkFileExists(LPCTSTR dirName) 
	{ 
		WIN32_FIND_DATA  data; 
		HANDLE handle = FindFirstFile(dirName,&data); 
		if (handle != INVALID_HANDLE_VALUE)
		{
			FindClose(handle);
			return true;
		}

		return false;
	}
	void TLServer_WM::Start() 
	{
		CString log_prefix("TLServer_WM::Start():");

		D(log_prefix + " testing D()");
		D1(log_prefix + " testing D1()");
		D2(log_prefix + " testing D2()");
		D3(log_prefix + " testing D3()");
		D4(log_prefix + " testing D4()");

		if (!ENABLED)
		{
			ENABLED = true;

			if (LOGENABLED)
			{
				// get log path
				TCHAR path[MAX_PATH];
				SHGetFolderPath(NULL,CSIDL_LOCAL_APPDATA,NULL,0,path);
				CString augpath;
				augpath.Format("%s\\%s",path,PROGRAM);
				// see if folder exists
				if (!checkFileExists(augpath.GetBuffer()))
					CreateDirectory(augpath,NULL);
				// get log file name
				std::vector<int> now;
				TLTimeNow(now);
				CString path_to_logfile;
				path_to_logfile.Format("%s\\%s.%i.txt",augpath,PROGRAM,now[0]);
				log.open(path_to_logfile, ios::app);

				CString debug_message;
				debug_message.Format("%s see the log: %s", log_prefix, path_to_logfile);  // dimon: let user see where the logs are
				D(debug_message);
			}

			CString servername = UniqueWindowName("TradeLinkServer");	// dimon: this name must match server window created in TLTransport.cs (c# portion - our clients ASP, Quotopia, etc.)) 
			CWnd* parent = CWnd::GetDesktopWindow();
			this->Create(NULL, servername, 0, CRect(0, 0, 20, 20), parent, NULL);
			this->ShowWindow(SW_HIDE); // hide our window
			//this->ShowWindow(SW_SHOW); // dimon: show our window ;)

			CString msg;
			msg.Format("%s Started %s [ %.1f.%i]", log_prefix, servername, MajorVer, MinorVer);
			this->D(msg);

			msg.Format("IB API Version:");
			this->D(msg);

			msg.Format("        API 9.69"); // todo: each time you manually upgrade TLServer to use newer api version, don't forget to replace it here..
			this->D(msg);

			msg.Format("        Release Date: July 1 2012");
			this->D(msg);
		}


	}



	long TLServer_WM::TLSend(int type,LPCTSTR msg,int clientid) 
	{
		// make sure client exists
		if ((clientid>=(int)hims.size()) || (clientid<0)) 
			return (long)TLCLIENT_NOT_FOUND;
		HWND dest = hims[clientid];
		return TLSend(type,msg,dest);
	}
	long TLServer_WM::TLSend(int type,LPCTSTR msg,CString windname) 
	{
		HWND dest = FindWindowA(NULL,(LPCSTR)(LPCTSTR)windname)->GetSafeHwnd();
		return TLSend(type,msg,dest);
	}
	long TLServer_WM::TLSend(int type,LPCTSTR msg, HWND dest)
	{
		// set default result
		LRESULT result = TLCLIENT_NOT_FOUND;

		if (dest) 
		{
			COPYDATASTRUCT CD;  // windows-provided structure for this purpose
			CD.dwData=type;		// stores type of message
			int len = 0;
			len = (int)strlen((char*)msg);

			CD.cbData = len+1;
			CD.lpData = (void*)msg;	//here's the data we're sending
			result = ::SendMessageA(dest,WM_COPYDATA,0,(LPARAM)&CD);
		} 
		return (long)result;
	}
}









