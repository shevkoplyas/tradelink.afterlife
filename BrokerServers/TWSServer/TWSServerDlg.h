// TWSServerDlg.h : header file
//

#pragma once
#include "afxwin.h"
#include "TWS_TLServer.h"
#include "afxcmn.h"


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
	CString NEWLINE;

	// Generated message map functions
	virtual BOOL OnInitDialog();
	afx_msg void OnSysCommand(UINT nID, LPARAM lParam);
	afx_msg void OnPaint();
	afx_msg void OnSize(UINT nType, int cx, int cy);
	afx_msg HCURSOR OnQueryDragIcon();
	afx_msg void OnVScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar); // dimon: todo: why this never fires up?
	DECLARE_MESSAGE_MAP()

	CRichEditCtrl m_status;
	CRichEditCtrl m_status1;
	CRichEditCtrl m_status2;
	CRichEditCtrl m_status3;
	CRichEditCtrl m_status4;
	bool sync_all_statuses_scrolling;

	TradeLibFast::TWS_TLServer* tl;

	int m_status_lines_count;
	int m_status1_lines_count;
	int m_status2_lines_count;
	int m_status3_lines_count;
	int m_status4_lines_count;
	CFont myMonospaceFont;

public:
	
	void status(LPCTSTR msg, int tltime=-1, int tltime_counter=-1);
	void status1(LPCTSTR msg, int tltime=-1, int tltime_counter=-1);
	void status2(LPCTSTR msg, int tltime=-1, int tltime_counter=-1);
	void status3(LPCTSTR msg, int tltime=-1, int tltime_counter=-1);
	void status4(LPCTSTR msg, int tltime=-1, int tltime_counter=-1);

	// these two will get/set value from/to cedit on our dialog
	int get_debuglevel();
	void set_debuglevel(int debug_level);

	int default_debug_level;

	afx_msg void OnDeltaposSpin1(NMHDR *pNMHDR, LRESULT *pResult);
	CEdit m_debuglevel;
	int min_debuglevel;
	int max_debuglevel;

	CStatic m_static0;
	CStatic m_static1;
	CStatic m_static2;
	CStatic m_static3;
	CStatic m_static4;
	afx_msg void OnEnVscrollStatus();
	CButton m_autoscroll;

	BOOL CTWSServerDlg::PreTranslateMessage(MSG* pMsg);
	afx_msg void OnEnVscrollStatus3();
};
