// TWSServerDlg.cpp : implementation file
//

#include "stdafx.h"
#include "TWSServer.h"
#include "TWSServerDlg.h"
#include "EClientSocket.h"   // C:\JTS\SocketClient\include must be added to include path
#include "TwsSocketClientErrors.h"   // C:\JTS\SocketClient\include must be added to include path

#ifdef _DEBUG
#define new DEBUG_NEW
#endif
using namespace TradeLibFast;

// CTWSServerDlg dialog

CTWSServerDlg::CTWSServerDlg(CWnd* pParent /*=NULL*/)
	: CDialog(CTWSServerDlg::IDD, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
	m_status_lines_count = 0;

	m_status1_lines_count = 0;
	m_status2_lines_count = 0;
	m_status3_lines_count = 0;
	m_status4_lines_count = 0;
	NEWLINE = "\r\n";
	//	NEWLINE = "\n";
	sync_all_statuses_scrolling = true; // dimon: todo: get this value from checkbox (what if it wal off and then became on again? how do we sync?)

	default_debug_level = 4;
	min_debuglevel = -1;
	max_debuglevel = 4;
}

void CTWSServerDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_STATUS, m_status);
	DDX_Control(pDX, IDC_STATUS1, m_status1);
	DDX_Control(pDX, IDC_STATUS2, m_status2);
	DDX_Control(pDX, IDC_STATUS3, m_status3);
	DDX_Control(pDX, IDC_STATUS4, m_status4);
	DDX_Control(pDX, IDC_EDIT_TLDEBUGLEVEL, m_debuglevel);
	DDX_Control(pDX, IDC_STATIC0, m_static0);
	DDX_Control(pDX, IDC_STATIC1, m_static1);
	DDX_Control(pDX, IDC_STATIC2, m_static2);
	DDX_Control(pDX, IDC_STATIC3, m_static3);
	DDX_Control(pDX, IDC_STATIC4, m_static4);
	DDX_Control(pDX, IDC_CHK_AUTOSCROLL, m_autoscroll);
}

BEGIN_MESSAGE_MAP(CTWSServerDlg, CDialog)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_SIZE()
	ON_WM_QUERYDRAGICON()
	ON_WM_VSCROLL()			// dimon: todo: why this never fires up?
	//}}AFX_MSG_MAP
	ON_NOTIFY(UDN_DELTAPOS, IDC_SPIN1, &CTWSServerDlg::OnDeltaposSpin1)
	ON_EN_VSCROLL(IDC_STATUS, &CTWSServerDlg::OnEnVscrollStatus)
	ON_EN_VSCROLL(IDC_STATUS3, &CTWSServerDlg::OnEnVscrollStatus3)
END_MESSAGE_MAP()


void CTWSServerDlg::OnSize(UINT nType, int cx, int cy)
{
	if (!tl) return;

	int number_of_debug_pannels = this->tl->TLDEBUG_LEVEL + 1;

	int cedit_width = (cx-0)/number_of_debug_pannels;
	int cedit_top = 67;
	int header_top = cedit_top-39;
	int cedit_height = (cy-cedit_top-0); // dimon: we'll reserve some space for additional controls here if needed

	if (m_status) {
		// dimon: make CEdit auto-resizable if we modify resize the window
		m_status.SetWindowPos(NULL,  cedit_width*0, cedit_top,  cedit_width, cedit_height, NULL);
		m_static0.SetWindowPos(NULL, cedit_width*0+10, header_top, cedit_width-15, header_top+7, NULL);
	}
	if (m_status1) {
		m_status1.SetWindowPos(NULL, cedit_width*1, cedit_top, cedit_width, cedit_height, NULL);
		m_static1.SetWindowPos(NULL, cedit_width*1+10, header_top, cedit_width-15, header_top+7, NULL);
	}
	if (m_status2) {
		m_status2.SetWindowPos(NULL, cedit_width*2, cedit_top, cedit_width, cedit_height, NULL);
		m_static2.SetWindowPos(NULL, cedit_width*2+10, header_top, cedit_width-15, header_top+7, NULL);
	}
	if (m_status3) {
		m_status3.SetWindowPos(NULL, cedit_width*3, cedit_top, cedit_width, cedit_height, NULL);
		m_static3.SetWindowPos(NULL, cedit_width*3+10, header_top, cedit_width-15, header_top+7, NULL);
	}
	if (m_status4) {
		m_status4.SetWindowPos(NULL, cedit_width*4, cedit_top, cedit_width, cedit_height+7, NULL);
		m_static4.SetWindowPos(NULL, cedit_width*4+10, header_top, cedit_width-15, header_top, NULL);
	}
}

// CTWSServerDlg message handlers
BOOL CTWSServerDlg::OnInitDialog()
{
	CDialog::OnInitDialog();

	m_autoscroll.SetCheck(true);

	myMonospaceFont.CreateFontA( 14, 0, 0, 0, FW_LIGHT, false, false, //  FW_HEAVY FW_LIGHT
		0, ANSI_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY,
		FIXED_PITCH|FF_MODERN, _T("Courier New") );

	// Set the icon for this dialog.  The framework does this automatically
	//  when the application's main window is not a dialog
	SetIcon(m_hIcon, TRUE);			// Set big icon
	SetIcon(m_hIcon, FALSE);		// Set small icon

	m_status.SetFont(&myMonospaceFont);
	m_status1.SetFont(&myMonospaceFont);
	m_status2.SetFont(&myMonospaceFont);
	m_status3.SetFont(&myMonospaceFont);
	m_status4.SetFont(&myMonospaceFont);

	m_status.SetEventMask(m_status.GetEventMask() | ENM_SCROLL | ENM_SCROLLEVENTS ); // dimon: to get notificaitions from RichEdit we have to SetEventMask() // http://msgroups.net/microsoft.public.vc.mfc/cricheditctrl-and-dynamic-scroll-bar/564427

	status("Starting tradelink.afterlife broker server...");
	tl = new TWS_TLServer();

	tl->TLDEBUG_LEVEL = default_debug_level; // dimon: see TLServer_WM.h for possible values

	__hook(&TLServer_WM::GotDebug,tl,&CTWSServerDlg::status); // dimon: for each event with type=TLServer::GotDebug originated from TWS_TLServer we call CTWSServerDlg::status, which just adds +1 line to self-scrolling CEdit field this->m_status
	__hook(&TLServer_WM::GotDebug1,tl,&CTWSServerDlg::status1);
	__hook(&TLServer_WM::GotDebug2,tl,&CTWSServerDlg::status2);
	__hook(&TLServer_WM::GotDebug3,tl,&CTWSServerDlg::status3);
	__hook(&TLServer_WM::GotDebug4,tl,&CTWSServerDlg::status4);

	// after instance of TWS_TLServer is created, but before tl->Start() we'd resize our dialog components (OnSize()) by calling SetWindowPos()
	ShowWindow(SW_NORMAL); // todo: after it is stable - get back to: SW_MINIMIZE);
	SetWindowPos(NULL, 0, 0, 1600, 800, NULL);	// dimon: change initial TWSServer dialog size
	set_debuglevel(default_debug_level);

	tl->Start();

	m_status.SetFocus();

	return TRUE;  // return TRUE  unless you set the focus to a control
}

CTWSServerDlg::~CTWSServerDlg()
{
	__unhook(&TLServer_WM::GotDebug,tl,&CTWSServerDlg::status);
	__unhook(&TLServer_WM::GotDebug1,tl,&CTWSServerDlg::status1);
	__unhook(&TLServer_WM::GotDebug2,tl,&CTWSServerDlg::status2);
	__unhook(&TLServer_WM::GotDebug3,tl,&CTWSServerDlg::status3);
	__unhook(&TLServer_WM::GotDebug4,tl,&CTWSServerDlg::status4);
	delete tl;
}

void CTWSServerDlg::OnSysCommand(UINT nID, LPARAM lParam)
{

	CDialog::OnSysCommand(nID, lParam);
}

// If you add a minimize button to your dialog, you will need the code below
//  to draw the icon.  For MFC applications using the document/view model,
//  this is automatically done for you by the framework.

void CTWSServerDlg::OnPaint()
{
	if (IsIconic())
	{
		CPaintDC dc(this); // device context for painting

		SendMessage(WM_ICONERASEBKGND, reinterpret_cast<WPARAM>(dc.GetSafeHdc()), 0);

		// Center icon in client rectangle
		int cxIcon = GetSystemMetrics(SM_CXICON);
		int cyIcon = GetSystemMetrics(SM_CYICON);
		CRect rect;
		GetClientRect(&rect);
		int x = (rect.Width() - cxIcon + 1) / 2;
		int y = (rect.Height() - cyIcon + 1) / 2;

		// Draw the icon
		dc.DrawIcon(x, y, m_hIcon);
	}
	else
	{
		CDialog::OnPaint();
	}
}

// The system calls this function to obtain the cursor to display while the user drags
//  the minimized window.
HCURSOR CTWSServerDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}


void CTWSServerDlg::status(LPCTSTR m, int tltime, int tltime_counter)
{
	// if called directly from TWSServer (not from TLServer_WM), then timestamp is missing. Get time:
	if (tltime < 0)
	{
		vector<int> now(2); // will hold 2 integers: date, time
		TLTimeNow(now);
		tltime = now[TLtime];
		tltime_counter = 0;
	}

	CString msg(m);
	if (msg == NEWLINE)
	{
		// if we got empty line (to be in sync with other edits), just add given timestamp
		;//msg.Format("%06d.%02d%s", tltime,tltime_counter, NEWLINE);
	}
	else
	{
		//	const CString NEWLINE = "\r\n";
		msg.Append(NEWLINE);
	}

	//// lame method of adding text to cedit:
	//CString stat;
	//m_status.GetWindowTextA(stat);
	//stat.Append(msg);
	//m_status.SetWindowTextA(stat);

	// good method of adding text to cedit (dimon:)
	// get the initial text length
	int nLength = m_status.GetWindowTextLengthA();
	// put the selection at the end of text
	m_status.SetSel(nLength, nLength);

	// replace the selection
	m_status.ReplaceSel(msg);

	m_status_lines_count++;

	if (m_autoscroll.GetCheck()) m_status.LineScroll(m_status_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE, tltime, tltime_counter);
		status2(NEWLINE, tltime, tltime_counter);
		status3(NEWLINE, tltime, tltime_counter);
		status4(NEWLINE, tltime, tltime_counter);
	}
}

void CTWSServerDlg::status1(LPCTSTR m, int tltime, int tltime_counter)
{
	// if called directly from TWSServer (not from TLServer_WM), then timestamp is missing. Get time:
	if (tltime < 0)
	{
		vector<int> now(2); // will hold 2 integers: date, time
		TLTimeNow(now);
		tltime = now[TLtime];
		tltime_counter = 0;
	}

	CString msg(m);
	if (msg == NEWLINE)
	{
		// if we got empty line (to be in sync with other edits), just add given timestamp
		;//msg.Format("%06d.%02d%s", tltime,tltime_counter, NEWLINE);
	}
	else
	{
		//	const CString NEWLINE = "\r\n";
		msg.Append(NEWLINE);
	}

	//CString stat;
	//m_status1.GetWindowTextA(stat);
	//stat.Append(msg);
	//m_status1.SetWindowTextA(stat);

	// good method of adding text to cedit (dimon:)
	// get the initial text length
	int nLength = m_status1.GetWindowTextLengthA();
	// put the selection at the end of text
	m_status1.SetSel(nLength, nLength);
	// replace the selection
	m_status1.ReplaceSel(msg);

	m_status1_lines_count++;

	if (m_autoscroll.GetCheck()) m_status1.LineScroll(m_status1_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status(NEWLINE, tltime, tltime_counter);
		status2(NEWLINE, tltime, tltime_counter);
		status3(NEWLINE, tltime, tltime_counter);
		status4(NEWLINE, tltime, tltime_counter);
	}
}

void CTWSServerDlg::status2(LPCTSTR m, int tltime, int tltime_counter)
{
	// if called directly from TWSServer (not from TLServer_WM), then timestamp is missing. Get time:
	if (tltime < 0)
	{
		vector<int> now(2); // will hold 2 integers: date, time
		TLTimeNow(now);
		tltime = now[TLtime];
		tltime_counter = 0;
	}

	CString msg(m);
	if (msg == NEWLINE)
	{
		// if we got empty line (to be in sync with other edits), just add given timestamp
		;//msg.Format("%06d.%02d%s", tltime,tltime_counter, NEWLINE);
	}
	else
	{
		//	const CString NEWLINE = "\r\n";
		msg.Append(NEWLINE);
	}

	//CString stat;
	//m_status2.GetWindowTextA(stat);
	//stat.Append(msg);
	//m_status2.SetWindowTextA(stat);

	// good method of adding text to cedit (dimon:)
	// get the initial text length
	int nLength = m_status2.GetWindowTextLengthA();
	// put the selection at the end of text
	m_status2.SetSel(nLength, nLength);
	// replace the selection
	m_status2.ReplaceSel(msg);

	m_status2_lines_count++;

	if (m_autoscroll.GetCheck()) m_status2.LineScroll(m_status2_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE, tltime, tltime_counter);
		status(NEWLINE, tltime, tltime_counter);
		status3(NEWLINE, tltime, tltime_counter);
		status4(NEWLINE, tltime, tltime_counter);
	}}

void CTWSServerDlg::status3(LPCTSTR m, int tltime, int tltime_counter)
{
	// if called directly from TWSServer (not from TLServer_WM), then timestamp is missing. Get time:
	if (tltime < 0)
	{
		vector<int> now(2); // will hold 2 integers: date, time
		TLTimeNow(now);
		tltime = now[TLtime];
		tltime_counter = 0;
	}

	CString msg(m);
	if (msg == NEWLINE)
	{
		// if we got empty line (to be in sync with other edits), just add given timestamp
		;//msg.Format("%06d.%02d%s", tltime,tltime_counter, NEWLINE);
	}
	else
	{
		//	const CString NEWLINE = "\r\n";
		msg.Append(NEWLINE);
	}

	//CString stat;
	//m_status3.GetWindowTextA(stat);
	//stat.Append(msg);
	//m_status3.SetWindowTextA(stat);

	// good method of adding text to cedit (dimon:)
	// get the initial text length
	int nLength = m_status3.GetWindowTextLengthA();
	// put the selection at the end of text
	m_status3.SetSel(nLength, nLength);
	// replace the selection
	m_status3.ReplaceSel(msg);

	m_status3_lines_count++;

	if (m_autoscroll.GetCheck()) m_status3.LineScroll(m_status3_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE, tltime, tltime_counter);
		status2(NEWLINE, tltime, tltime_counter);
		status(NEWLINE, tltime, tltime_counter);
		status4(NEWLINE, tltime, tltime_counter);
	}}

void CTWSServerDlg::status4(LPCTSTR m, int tltime, int tltime_counter)
{
	// if called directly from TWSServer (not from TLServer_WM), then timestamp is missing. Get time:
	if (tltime < 0)
	{
		vector<int> now(2); // will hold 2 integers: date, time
		TLTimeNow(now);
		tltime = now[TLtime];
		tltime_counter = 0;
	}

	CString msg(m);
	if (msg == NEWLINE)
	{
		// if we got empty line (to be in sync with other edits), just add given timestamp
		;//msg.Format("%06d.%02d%s", tltime,tltime_counter, NEWLINE);
	}
	else
	{
		//	const CString NEWLINE = "\r\n";
		msg.Append(NEWLINE);
	}

	//CString stat;
	//m_status4.GetWindowTextA(stat);
	//stat.Append(msg);
	//m_status4.SetWindowTextA(stat);

	// good method of adding text to cedit (dimon:)
	// get the initial text length
	int nLength = m_status4.GetWindowTextLengthA();
	// put the selection at the end of text
	m_status4.SetSel(nLength, nLength);
	// replace the selection
	m_status4.ReplaceSel(msg);

	m_status4_lines_count++;

	if (m_autoscroll.GetCheck()) m_status4.LineScroll(m_status4_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE, tltime, tltime_counter);
		status2(NEWLINE, tltime, tltime_counter);
		status3(NEWLINE, tltime, tltime_counter);
		status(NEWLINE, tltime, tltime_counter);
	}
}


void CTWSServerDlg::OnDeltaposSpin1(NMHDR *pNMHDR, LRESULT *pResult)
{
	LPNMUPDOWN pNMUpDown = reinterpret_cast<LPNMUPDOWN>(pNMHDR);

	int curr_val = get_debuglevel();
	set_debuglevel(curr_val - pNMUpDown->iDelta);

	Invalidate();

	*pResult = 0;
}

int CTWSServerDlg::get_debuglevel()
{
	CString curr_val;
	m_debuglevel.GetWindowTextA( curr_val);
	return atoi(curr_val);
}

void CTWSServerDlg::set_debuglevel(int debug_level)
{
	CString new_val;
	new_val.Format("%d", debug_level);
	if (debug_level >= min_debuglevel && debug_level <= max_debuglevel)
		m_debuglevel.SetWindowTextA(new_val);	
}

void CTWSServerDlg::OnEnVscrollStatus()
{
	int lineNumber = m_status.GetFirstVisibleLine();

	int nPos = m_status.GetScrollPos(SB_VERT);

	//int delta1 = nPos - m_status1.GetScrollPos(SB_VERT);
	//int delta2 = nPos - m_status2.GetScrollPos(SB_VERT);
	//int delta3 = nPos - m_status3.GetScrollPos(SB_VERT);
	//int delta4 = nPos - m_status4.GetScrollPos(SB_VERT);

	int delta1 = lineNumber - m_status1.GetFirstVisibleLine();
	int delta2 = lineNumber - m_status2.GetFirstVisibleLine();
	int delta3 = lineNumber - m_status3.GetFirstVisibleLine();
	int delta4 = lineNumber - m_status4.GetFirstVisibleLine();

	m_status1.LineScroll(delta1); // adjust scrollbar for all edit boxes
	m_status2.LineScroll(delta2);
	m_status3.LineScroll(delta3);
	m_status4.LineScroll(delta4);

	//m_status1.SetScrollPos(SB_VERT, nPos, 1); // adjust text position for all edit boxes
	//m_status2.SetScrollPos(SB_VERT, nPos, 1);
	//m_status3.SetScrollPos(SB_VERT, nPos, 1);
	//m_status4.SetScrollPos(SB_VERT, nPos, 1);

}
void CTWSServerDlg::OnVScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar)
{
	// dimon: todo: why this never fires up?
}


BOOL CTWSServerDlg::PreTranslateMessage(MSG* pMsg)
{
	if( pMsg->message == WM_KEYDOWN )
	{
		if(pMsg->wParam == VK_RETURN || pMsg->wParam == VK_ESCAPE)
		{
			return TRUE;                // Do not process further. This sill supress connector close on ESC button!
		}
	}
	else if (pMsg->message == WM_COMMAND )
	{
		m_static2.SetWindowTextA("asdf");
	}

	return CWnd::PreTranslateMessage(pMsg);
}


void CTWSServerDlg::OnEnVscrollStatus3()
{
	// TODO: Add your control notification handler code here
}
