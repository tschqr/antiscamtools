using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.IO;

namespace TreeForScammers
{
  public class ChatWindow : Form
  {
    private Label attribution;
    private TextBox chatbox;
    private ToolTip tip;

    public ChatWindow(string[] args)
    {
      this.Shown += new System.EventHandler(this.handle_formshown);
      this.FormClosed +=
        new System.Windows.Forms.FormClosedEventHandler(this.handle_formclose);

      int win_x = -1, win_y = -1, win_width = 600, win_height = 200;
      if(args.Length == 4) {
        /* arguments are suggested window position */
        int sugg_x, sugg_y, sugg_width, sugg_height;
        if(int.TryParse(args[0], out sugg_x) &&
           int.TryParse(args[1], out sugg_y) &&
           int.TryParse(args[2], out sugg_width) &&
           int.TryParse(args[3], out sugg_height)) {
          win_x = sugg_x;
          win_y = sugg_y;
          win_width = sugg_width;
          win_height = sugg_height;
        }
      }

      this.SuspendLayout();
      this.Name = "tree";
      this.Width = win_width;
      this.Height = win_height;
      this.MinimumSize = new Size(400, 200);
      if(win_x >= 0 && win_y >= 0) {
        this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        this.Location = new System.Drawing.Point(win_x, win_y);
      }
      this.ControlBox = false;

      /* this.Text goes in the title bar and the Alt-Tab switcher. The
         title bar is disabled (see WS_CAPTION below) so this is just
         for Alt-Tab. */
      this.Text = "Tree chat";

      try {
        this.Icon = new Icon(
          Path.Combine(
            Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
            "streecw.ico"
          )
        );
      } catch {
        /* Oh well, keep going */
      }

      attribution = new Label();
      attribution.Text = "Somebody is typing:";
      attribution.Width = this.ClientRectangle.Width;
      attribution.Anchor =
        AnchorStyles.Top |
        AnchorStyles.Left | AnchorStyles.Right;
      attribution.TextAlign = ContentAlignment.MiddleCenter;
      this.Controls.Add(attribution);

      chatbox = new TextBox();
      chatbox.Width = this.ClientRectangle.Width;
      chatbox.Left = 0;
      chatbox.Height = this.ClientRectangle.Height - attribution.Height;
      chatbox.Top = attribution.Height;
      chatbox.Anchor =
        AnchorStyles.Top | AnchorStyles.Bottom |
        AnchorStyles.Left | AnchorStyles.Right;
      chatbox.Multiline = true;
      chatbox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
      chatbox.WordWrap = true;
      chatbox.HideSelection = false;
      chatbox.AcceptsReturn = true;
      chatbox.AcceptsTab = true;
      chatbox.ContextMenu = new ContextMenu();
      chatbox.Font = new Font(
        chatbox.Font.Name, /* change to "Comic Sans MS" if you dare */
        chatbox.Font.Size*2,
        chatbox.Font.Style,
        chatbox.Font.Unit
      );
      chatbox.KeyDown +=
        new System.Windows.Forms.KeyEventHandler(this.handle_chatbox_keydown);
      chatbox.KeyPress +=
        new System.Windows.Forms.KeyPressEventHandler(
          this.handle_chatbox_keypress);
      this.Controls.Add(chatbox);

      tip = new ToolTip();
      tip.Popup += new PopupEventHandler(this.handle_tip_popup);
      tip.Draw += new DrawToolTipEventHandler(this.handle_tip_draw);
      tip.OwnerDraw = true;

      this.ResumeLayout();

      /* I have to start a thread to handle stdin? Argh. There will
         probably be bugs. The correct design is XtAppAddInput. */
      new Thread(stdin_readloop).Start();
    }

    /* kill title bar */
    protected override System.Windows.Forms.CreateParams CreateParams
    {
      get
      {
        System.Windows.Forms.CreateParams cp = base.CreateParams;
        cp.Style &= ~0x00C00000; /* WS_CAPTION */
        return cp;
      }
    }

    private void handle_formshown(Object sender, EventArgs e)
    {
      this.Activate();
      this.chatbox.Focus();
    }

    private void handle_formclose(Object sender, EventArgs e)
    {
      Environment.Exit(0);
    }

    private bool forbidkey = false;
    private string forbidkey_msg = null;
    private static string nobksp_fontfamily = "Consolas";
    private static int nobksp_fontsize = 28;

    private void handle_chatbox_keydown(Object sender, KeyEventArgs e)
    {
      forbidkey = false;
      Console.WriteLine("keydown [{0}][{1}][{2}]", e.KeyCode, e.KeyData, e.KeyValue);
      if(e.KeyCode == Keys.Back) {
        forbidkey = true;
        forbidkey_msg = "Backspace key disabled!";
      } else if(e.KeyCode == Keys.Delete) {
        forbidkey = true;
        forbidkey_msg = "Delete key disabled!";
        /* This one doesn't generate a keypress event */
        e.Handled = true;
        show_forbidkey_tip();
      } else if(e.KeyCode == Keys.Z && e.Control) {
        forbidkey = true;
        forbidkey_msg = "Undo disabled!";
      } else if(e.KeyCode == Keys.X && e.Control &&
                this.chatbox.SelectedText.Length > 0) {
        forbidkey = true;
        forbidkey_msg = "Cut disabled!";
      }
    }

    private void handle_chatbox_keypress(Object sender, KeyPressEventArgs e)
    {
      Console.WriteLine("keypress {0}", e.KeyChar);
      if(forbidkey) {
        e.Handled = true;
        show_forbidkey_tip();
        return;
      }
      chatbox_deselect_append();

      /* Want to clear the visible text area by hitting Enter
         repeatedly until it scrolls? Nope! Blank lines not allowed. */
      if(e.KeyChar == '\n' || e.KeyChar == '\r') {
        if(this.chatbox.SelectionStart == 0) {
          e.Handled = true;
          return;
        }
        char prevchar = this.chatbox.Text[this.chatbox.SelectionStart-1];
        if(prevchar =='\n' || prevchar=='\r') {
          e.Handled = true;
          return;
        }
      }
    }

    private void show_forbidkey_tip()
    {
      /* We have to choose the tip location here, before we know the
         size, but we want to center it. Oh well, we can calculate the
         size twice. */
      using(Font f = new Font(nobksp_fontfamily, nobksp_fontsize)) {
        Size sz = TextRenderer.MeasureText(forbidkey_msg, f);
        this.tip.Show(
          forbidkey_msg,
          this.chatbox,
          (this.chatbox.Width - sz.Width)/2,
          (this.chatbox.Height - sz.Height)/2,
          750);
      }
    }

    private void chatbox_deselect_append()
    {
      /* Want to delete text by selecting it and then typing a
         replacement? You will just append! */
      if(this.chatbox.SelectedText.Length > 0) {
        if(!Char.IsWhiteSpace(this.chatbox.Text[this.chatbox.Text.Length-1]))
          this.chatbox.Text += " ";
        this.chatbox.SelectionStart = this.chatbox.Text.Length;
        this.chatbox.SelectionLength = 0;
      }
    }

    private void chatbox_insert_text(string t)
    {
      chatbox_deselect_append();
      int old_ss = this.chatbox.SelectionStart;
      this.chatbox.Text =
        this.chatbox.Text.Substring(0, old_ss) +
        t +
        this.chatbox.Text.Substring(old_ss);
      this.chatbox.SelectionStart = old_ss + t.Length;
      this.chatbox.SelectionLength = 0;
      this.chatbox.ScrollToCaret();
    }

    private void handle_tip_popup(Object sender, PopupEventArgs e)
    {
      using(Font f = new Font(nobksp_fontfamily, nobksp_fontsize)) {
        e.ToolTipSize =
          TextRenderer.MeasureText(tip.GetToolTip(e.AssociatedControl), f);
      }
    }

    private static Color[][] tip_color_list = new Color[][] {
      new Color[] { Color.Red, Color.White },
      new Color[] { Color.Yellow, Color.Black },
      new Color[] { Color.Green, Color.White },
      new Color[] { Color.LightBlue, Color.Black },
      new Color[] { Color.Purple, Color.White }
    };
    private static int tip_next_color = 0;

    private void handle_tip_draw(Object sender, DrawToolTipEventArgs e)
    {
      int cidx = tip_next_color++;
      if(tip_next_color >= tip_color_list.Length)
        tip_next_color = 0;
      Color bgcolor = tip_color_list[cidx][0];
      Color fgcolor = tip_color_list[cidx][1];

      using(SolidBrush bg = new SolidBrush(bgcolor)) {
        e.Graphics.FillRectangle(bg, e.Bounds);
      }
      e.DrawBorder();
      using(StringFormat sf = new StringFormat()) {
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment =StringAlignment.Center;
        sf.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
        sf.FormatFlags = StringFormatFlags.NoWrap;
        using(Font f = new Font(nobksp_fontfamily, nobksp_fontsize))
        using(SolidBrush fg = new SolidBrush(fgcolor)) {
          e.Graphics.DrawString(e.ToolTipText, f, fg, e.Bounds, sf);
        }
      }
    }

    private void stdin_readloop()
    {
      try {
        string kdesc_str;
        while((kdesc_str = Console.ReadLine()) != null) {
          string[] kdesc = kdesc_str.Split(':');
          if(kdesc.Length != 3)
            return;
          ConsoleKey key;
          int keychar_int;
          char keychar;
          ConsoleModifiers modifiers;
          if(!Enum.TryParse(kdesc[0], out key))
            return;
          if(!int.TryParse(kdesc[1], out keychar_int))
            return;
          keychar=(char)keychar_int;
          if(!Enum.TryParse(kdesc[2], out modifiers))
            return;

          /* key, keychar, and modifiers are now the original
             ConsoleKeyInfo property values. Unfortunately it's not easy
             to send those keys to the TextBox. chatbox.OnKeyPress()
             can't be called from here, and SendKeys.Send() has no
             target selection ability at all, so we can't even get far
             enough to be annoyed by the necessary format conversion.
             So here's a simple simulation that should be correct for
             printable characters and a few other keys, and wrong in an
             understandable way for the rest. */
          if(keychar != '\u0000' && (!Char.IsControl(keychar))) {
            chatbox_insert_text(keychar.ToString());
          } else if(key == ConsoleKey.Backspace) {
            /* would like to do this:
                 forbidkey_msg = "Backspace key disabled!";
                 show_forbidkey_tip();
               and similar for Delete, Ctrl-Z, and Ctrl-X but
               show_forbidkey_tip() doesn't work here, for unknown
               reasons. Child threads are not allowed to show tooltips? */
            chatbox_insert_text(
              "<" +
              (modifiers == 0 ? "" : "("+ modifiers.ToString() + ")") +
              "BkSp" +
              ">"
            );
          } else {
            chatbox_insert_text(
              "<" +
              (modifiers == 0 ? "" : "("+ modifiers.ToString() + ")") +
              key.ToString() +
              ">"
            );
          }
        }
      } catch {
        /* If anything weird happens, just give up on stdin. */
        return;
      }
    }
  }
}
