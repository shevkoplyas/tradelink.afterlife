#pragma once
#include "TradeLibFast.h"
#include <vector>
#include <fstream>
using namespace std;

namespace TradeLibFast
{
const int MAXTICKS = 10000;		
	// TLServer_WM

	[event_source(native)]
	class AFX_EXT_CLASS  TLServer_WM : public CWnd
	{

	public:
		TLServer_WM(void);
		~TLServer_WM(void);
		int TLDEBUG_LEVEL;
		bool ENABLED;
		bool LOGENABLED;
		__event void GotDebug(LPCTSTR msg);
		__event void GotDebug1(LPCTSTR msg); // dimon: levels 1-4 are sort of "more verbose" channels
		__event void GotDebug2(LPCTSTR msg); // which can be usefull to show all the messages flying by
		__event void GotDebug3(LPCTSTR msg); // normally should be turned off
		__event void GotDebug4(LPCTSTR msg); // normally should be turned off
		CString debugbuffer;
		long TLSend(int type,LPCTSTR msg, int clientid);
		static long TLSend(int type,LPCTSTR msg, HWND dest);
		static long TLSend(int type,LPCTSTR msg,CString windname);
		void SrvGotOrder(TLOrder order);
		void SrvGotFill(TLTrade trade);
		void SrvGotTick(TLTick tick);
		void SrvGotTickAsync(TLTick k);
		void SrvGotCancel(int64 orderid);
		CString Version();
		int FindSym(CString sym);
		// thread stuff
		volatile uint _writeticks;
		volatile uint _readticks;
		volatile bool _tickflip;
		volatile bool _go;
		volatile bool _startthread;

		vector<TLTick> _tickcache;

	protected:
		ofstream log;
		CString PROGRAM;
		bool needStock(CString stock);
		int FindClient(CString clientname);
		double MajorVer;
		int MinorVer;

		vector<int> imbclient;
		vector <CString>client; // map client ids to name
		vector<HWND>hims; // store client handles
		vector <time_t>heart; // map last contact to id
		typedef vector <CString> clientstocklist; // hold a single clients stocks
		vector < clientstocklist > stocks; // map stocklist to id
		typedef vector<int> clientindex; // points to clients
		vector<clientindex> symclientidx; // points which clients have a symbol
		vector<CString> symindex;

		bool ClientHasSymbol(int clientid, CString sym);
		void IndexBaskets();



		
		DECLARE_MESSAGE_MAP()

	public:
		afx_msg BOOL OnCopyData(CWnd* pWnd, COPYDATASTRUCT* pCopyDataStruct);
		int RegisterClient(CString  clientname);
		int HeartBeat(CString clientname);
		virtual int UnknownMessage(int MessageType, CString msg);
		virtual int BrokerName(void);
		virtual int SendOrder(TLOrder order);
		virtual int AccountResponse(CString clientname);
		virtual int PositionResponse(CString account, CString clientname);
		virtual int RegisterStocks(CString clientname);
		virtual int DOMRequest(int depth);
		virtual std::vector<int> GetFeatures();
		virtual int ClearClient(CString client);
		virtual int ClearStocks(CString client);
		virtual int CancelRequest(int64 order);
		virtual void Start();

		void D(const CString & message);
		void D1(const CString & message);
		void D2(const CString & message);
		void D3(const CString & message);
		void D4(const CString & message);

		bool HaveSubscriber(CString stock);
		
	};


}
