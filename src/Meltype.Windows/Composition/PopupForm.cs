// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

namespace Meltype.Composition;

/// <summary>枠もタスクバーのボタンも無く、常に手前に出て、フォーカスを奪わない小さなウィンドウ (変換ボックス・意味・入力モードの表示)。</summary>
internal abstract class PopupForm : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_TOPMOST = 0x00000008, WS_EX_TRANSPARENT = 0x00000020;

    protected PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        // 最前面には CreateParams の WS_EX_TOPMOST を使う。
        // Form.TopMost を使うと、WinForms が表示時に SetFocus を呼んで入力欄のフォーカスを奪う。
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
    }

    /// <summary>bounds に出し、最前面のウィンドウの中でいちばん手前に置き直す。出すときも、出したまま中身を変えるときも、これで出す。</summary>
    public void ShowAt(Rectangle bounds)
    {
        SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        if (!Visible) Show();
        // WS_EX_TOPMOST は作ったときに一度付くだけで、重なり順は後から動く。印が付いたまま普通のウィンドウより下に落ちると、
        // 出しても入力欄のウィンドウの裏に隠れ、作り直す (Meltype を起動し直す) まで見えなかった。
        // TIP の候補ウィンドウ (native/tip/CandidateWindow.cpp) と同じく、出すたびに置き直して、それまでの重なり順に頼らない。
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>クリックを下のウィンドウへ通すか。</summary>
    protected virtual bool ClickThrough => false;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            if (ClickThrough) cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    private const int WM_ACTIVATE = 0x0006, WA_INACTIVE = 0;
    private const int WM_MOUSEACTIVATE = 0x0021, MA_NOACTIVATE = 3;

    protected override void WndProc(ref Message m)
    {
        // Windows の「マウスを合わせたウィンドウをアクティブにする」は WS_EX_NOACTIVATE を見ない。
        // アクティブになる前に断り、クリック自体は消さない (候補の操作を妨げない)。
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        // 別の経路でアクティブにされた場合は、前にアクティブだったウィンドウ (入力欄) に返す。
        // 返さないと、確定した文字が入力欄に届かずに消える (issue #997)
        if (m.Msg == WM_ACTIVATE && (m.WParam.ToInt64() & 0xFFFF) != WA_INACTIVE && m.LParam != IntPtr.Zero)
        {
            base.WndProc(ref m);
            SetForegroundWindow(m.LParam);
            return;
        }
        base.WndProc(ref m);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
