using System;
using System.Windows.Forms;

namespace TreeForScammers
{
  public class ChatWindowProg
  {
    [STAThread]
    public static void Main(string[] args)
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new ChatWindow(args));
    }
  }
}
