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
	sync_all_statuses_scrolling = true; // dimon: todo: get this value from checkbox (what if it wal off and then became on again? how do we sync?)
}

void CTWSServerDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_STATUS, m_status);
	DDX_Control(pDX, IDC_STATUS1, m_status1);
	DDX_Control(pDX, IDC_STATUS2, m_status2);
	DDX_Control(pDX, IDC_STATUS3, m_status3);
	DDX_Control(pDX, IDC_STATUS4, m_status4);
}

BEGIN_MESSAGE_MAP(CTWSServerDlg, CDialog)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_SIZE()
	ON_WM_QUERYDRAGICON()
	//}}AFX_MSG_MAP
END_MESSAGE_MAP()


void CTWSServerDlg::OnSize(UINT nType, int cx, int cy)
{
	if (!tl) return;

	int number_of_debug_pannels = this->tl->TLDEBUG_LEVEL + 1;

	int cedit_width = (cx-0)/number_of_debug_pannels;
	int cedit_top = 60;
	int cedit_height = (cy-cedit_top-0); // dimon: we'll reserve some space for additional controls here if needed

	if (m_status) {
		// dimon: make CEdit auto-resizable if we modify resize the window
		m_status.SetWindowPos(NULL, cedit_width*0, cedit_top, cedit_width, cedit_height, NULL);// SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER);
	}
	if (m_status1) {
		m_status1.SetWindowPos(NULL, cedit_width*1, cedit_top, cedit_width, cedit_height, NULL);// SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER);
	}
	if (m_status2) {
		m_status2.SetWindowPos(NULL, cedit_width*2, cedit_top, cedit_width, cedit_height, NULL);// SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER);
	}
	if (m_status3) {
		m_status3.SetWindowPos(NULL, cedit_width*3, cedit_top, cedit_width, cedit_height, NULL);// SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER);
	}
	if (m_status4) {
		m_status4.SetWindowPos(NULL, cedit_width*4, cedit_top, cedit_width, cedit_height, NULL);// SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER);
	}
}

// CTWSServerDlg message handlers

BOOL CTWSServerDlg::OnInitDialog()
{
	CDialog::OnInitDialog();


	// Set the icon for this dialog.  The framework does this automatically
	//  when the application's main window is not a dialog
	SetIcon(m_hIcon, TRUE);			// Set big icon
	SetIcon(m_hIcon, FALSE);		// Set small icon

	status("Starting tradelink.afterlife broker server...");
	tl = new TWS_TLServer();

	tl->TLDEBUG_LEVEL = 4; // dimon: see TLServer_WM.h for possible values

	__hook(&TLServer_WM::GotDebug,tl,&CTWSServerDlg::status); // dimon: for each event with type=TLServer::GotDebug originated from TWS_TLServer we call CTWSServerDlg::status, which just adds +1 line to self-scrolling CEdit field this->m_status
	__hook(&TLServer_WM::GotDebug1,tl,&CTWSServerDlg::status1);
	__hook(&TLServer_WM::GotDebug2,tl,&CTWSServerDlg::status2);
	__hook(&TLServer_WM::GotDebug3,tl,&CTWSServerDlg::status3);
	__hook(&TLServer_WM::GotDebug4,tl,&CTWSServerDlg::status4);

	// after instance of TWS_TLServer is created, but before tl->Start() we'd resize our dialog components (OnSize()) by calling SetWindowPos()
	ShowWindow(SW_NORMAL); // todo: after it is stable - get back to: SW_MINIMIZE);
	SetWindowPos(NULL, 0, 0, 1600, 800, NULL);	// dimon: change initial TWSServer dialog size

	tl->Start();

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

//void CTWSServerDlg::cstat(CString msg)
//{
//	const CString NEWLINE = "\r\n";
//	CString stat;
//	m_status.GetWindowTextA(stat);
//	stat.Append(msg+NEWLINE);
//	m_status.SetWindowTextA(stat);
//}


void CTWSServerDlg::status(LPCTSTR m)
{
	CString msg(m);
	const CString NEWLINE = "\r\n";
	msg.Append(NEWLINE);
	CString stat;
	m_status.GetWindowTextA(stat);
	stat.Append(msg);
	m_status.SetWindowTextA(stat);
	m_status_lines_count++;
	m_status.LineScroll(m_status_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE);
		status2(NEWLINE);
		status3(NEWLINE);
		status4(NEWLINE);
	}
}

void CTWSServerDlg::status1(LPCTSTR m)
{
	CString msg(m);
	const CString NEWLINE = "\r\n";
	msg.Append(NEWLINE);
	CString stat;
	m_status1.GetWindowTextA(stat);
	stat.Append(msg);
	m_status1.SetWindowTextA(stat);
	m_status1_lines_count++;
	m_status1.LineScroll(m_status1_lines_count);
	
	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status(NEWLINE);
		status2(NEWLINE);
		status3(NEWLINE);
		status4(NEWLINE);
	}
}

void CTWSServerDlg::status2(LPCTSTR m)
{
	CString msg(m);
	
	msg.Append(NEWLINE);
	CString stat;
	m_status2.GetWindowTextA(stat);
	stat.Append(msg);
	m_status2.SetWindowTextA(stat);
	m_status2_lines_count++;
	m_status2.LineScroll(m_status2_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE);
		status(NEWLINE);
		status3(NEWLINE);
		status4(NEWLINE);
	}}

void CTWSServerDlg::status3(LPCTSTR m)
{
	CString msg(m);
	const CString NEWLINE = "\r\n";
	msg.Append(NEWLINE);
	CString stat;
	m_status3.GetWindowTextA(stat);
	stat.Append(msg);
	m_status3.SetWindowTextA(stat);
	m_status3_lines_count++;
	m_status3.LineScroll(m_status3_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE);
		status2(NEWLINE);
		status(NEWLINE);
		status4(NEWLINE);
	}}

void CTWSServerDlg::status4(LPCTSTR m)
{
	CString msg(m);
	const CString NEWLINE = "\r\n";
	msg.Append(NEWLINE);
	CString stat;
	m_status4.GetWindowTextA(stat);
	stat.Append(msg);
	m_status4.SetWindowTextA(stat);
	m_status4_lines_count++;
	m_status4.LineScroll(m_status4_lines_count);

	if (sync_all_statuses_scrolling && m != NEWLINE)
	{
		status1(NEWLINE);
		status2(NEWLINE);
		status3(NEWLINE);
		status(NEWLINE);
	}
}
