// TWSServerDlg.h : header file
//

#pragma once
#include "afxwin.h"
#include "TWS_TLServer.h"


// CTWSServerDlg dialog
[event_receiver(native)]
class CTWSServerDlg : public CDialog
{
// Construction
public:
	CTWSServerDlg(CWnd* pParent = NULL);	// standard constructor
	~CTWSServerDlg();

// Dialog Data
	enum { IDD = IDD_TWSSERVER_DIALOG };

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV support


// Implementation
protected:
	HICON m_hIcon;
	CString NEWLINE;// = "\r\n";

	// Generated message map functions
	virtual BOOL OnInitDialog();
	afx_msg void OnSysCommand(UINT nID, LPARAM lParam);
	afx_msg void OnPaint();
	afx_msg void OnSize(UINT nType, int cx, int cy);
	afx_msg HCURSOR OnQueryDragIcon();
	DECLARE_MESSAGE_MAP()
	
	CEdit m_status;
	CEdit m_status1;
	CEdit m_status2;
	CEdit m_status3;
	CEdit m_status4;
	bool sync_all_statuses_scrolling;
	
	TradeLibFast::TWS_TLServer* tl;
	
	int m_status_lines_count;
	int m_status1_lines_count;
	int m_status2_lines_count;
	int m_status3_lines_count;
	int m_status4_lines_count;
public:
	//void cstat(CString msg);
	
	void status(LPCTSTR msg);
	void status1(LPCTSTR msg);
	void status2(LPCTSTR msg);
	void status3(LPCTSTR msg);
	void status4(LPCTSTR msg);

};
