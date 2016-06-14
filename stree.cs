using System;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

namespace TreeForScammers
{
  public class TreeForScammers
  {
    [DllImportAttribute("kernel32.dll", SetLastError = true)]
    static extern bool GetVolumeInformation(
      string Volume, StringBuilder VolumeName, uint VolumeNameSize,
      out uint SerialNumber, out uint MaximumComponentLength, out uint flags,
      StringBuilder fs, uint fs_size);

    [DllImport("User32")]
    private static extern int SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
    }

    static private Process chatproc;
    static private bool aflag = false;
    static private bool fflag = false;

    public static void Main(string[] args)
    {
      string dir_arg = null;

      foreach(string arg in args) {
        if(arg == "/a" || arg == "/A") {
          if(aflag) {
            Console.Error.WriteLine("Parameter format not correct - {0}", arg);
            Environment.Exit(0);
          }
          aflag = true;
        } else if(arg == "/f" || arg == "/F") {
          if(fflag) {
            Console.Error.WriteLine("Parameter format not correct - {0}", arg);
            Environment.Exit(0);
          }
          fflag = true;
        } else if(arg == "/?") {
          Console.Write(
"Graphically displays the folder structure of a drive or path.\r\n" +
"\r\n" +
"TREE [drive:][path] [/F] [/A]\r\n" +
"\r\n" +
"   /F   Display the names of the files in each folder.\r\n" +
"   /A   Use ASCII instead of extended characters.\r\n"
          );
          Environment.Exit(0);
        } else if(arg.StartsWith("/")) {
          if(arg.Length == 2)
            Console.Error.WriteLine("Invalid switch - {0}", arg);
          else
            Console.Error.WriteLine("Parameter format not correct - {0}", arg);
          Environment.Exit(0);
        } else {
          if(dir_arg != null) {
            Console.Error.WriteLine("Too many parameters - {0}", arg);
            Environment.Exit(0);
          }
          dir_arg = arg;
        }
      }

      /* Imitate the header of the real tree */
      string treeroot = dir_arg != null ? dir_arg : ".";
      string driveroot = Path.GetPathRoot(treeroot);
      if(driveroot == "")
        driveroot = Path.GetPathRoot(Environment.CurrentDirectory);
      StringBuilder VolLabel = new StringBuilder(256);
      uint serNum = 0;
      uint maxCompLen = 0;
      UInt32 VolFlags = new UInt32();
      StringBuilder FSName = new StringBuilder(256);
      if(!GetVolumeInformation(driveroot, VolLabel, (uint)VolLabel.Capacity,
                               out serNum, out maxCompLen, out VolFlags,
                               FSName, (uint)FSName.Capacity)) {
        /* Can't find real info, so fake it. Somehow the real tree
           command can get a name and serial number for UNC paths, but
           it doesn't work here. */
        VolLabel.Clear().Append("DATA");
        Random rnd = new Random();
        serNum = (((uint)rnd.Next(1 << 30)) << 2) | ((uint)rnd.Next(1 << 2));
      }
      Console.WriteLine("Folder PATH listing for volume {0}", VolLabel);
      Console.WriteLine(
        "Volume serial number is {0:X4}-{1:X4}", serNum>>16, serNum&0xffff);

      string treeroot_fullpath = Path.GetFullPath(treeroot);

      Console.WriteLine(
        "{0}", (
          dir_arg == null ?
          driveroot.Replace("\\", "") + "." :
          treeroot_fullpath + (
            Regex.IsMatch(treeroot_fullpath, @"^\\\\[^\\]*\\[^\\]*$") ? "." : ""
          )
        ).ToUpper()
      );

      string treeroot_fullpath_without_drive = treeroot_fullpath;
      if(treeroot_fullpath.ToUpper().StartsWith(driveroot.ToUpper())) {
        treeroot_fullpath_without_drive =
          treeroot_fullpath.Substring(driveroot.Length);
        if(!treeroot_fullpath_without_drive.StartsWith("\\"))
          treeroot_fullpath_without_drive =
            "\\" + treeroot_fullpath_without_drive;
      }

      if(!Directory.Exists(treeroot)) {
        Console.WriteLine(
          "Invalid path - {0}",
          treeroot_fullpath_without_drive.ToUpper());
        Console.WriteLine("No subfolders exist ");
        Console.WriteLine("");
        Environment.Exit(0);
      }

      if(!showdir(treeroot)) {
        Console.WriteLine("No subfolders exist ");
        Console.WriteLine("");
      }
    }

    private struct prefixes
    {
      public string notlast;
      public string last;
      public string notlast_recurse;
      public string last_recurse;
    }

    static private prefixes[] all_prefixes = {
      new prefixes {
        notlast = "\x251C\x2500\x2500\x2500",
        last = "\x2514\x2500\x2500\x2500",
        notlast_recurse = "\x2502   ",
        last_recurse = "    "
      },
      new prefixes {
        notlast = "+---",
        last = "\\---",
        notlast_recurse = "|   ",
        last_recurse = "    "
      }
    };

    private static bool showdir(string root, string indent = "")
    {
      prefixes pfx = all_prefixes[aflag?1:0];
      bool printed_a_subdir = false;
      try {
        string[] subdirs = Directory.GetDirectories(root);
        if(fflag) {
          bool printed_a_file = false;
          string[] files = Directory.GetFiles(root);
          string file_pfx =
            subdirs.Length > 0 ? pfx.notlast_recurse : pfx.last_recurse;
          foreach(string file in files) {
            FileAttributes attr = File.GetAttributes(file);
            if((attr & (FileAttributes.Hidden | FileAttributes.System)) == 0) {
              say("{0}{1}{2}", indent, file_pfx, Path.GetFileName(file));
              printed_a_file = true;
            }
          }
          if(printed_a_file)
            say("{0}{1}", indent, file_pfx);
        }
        for(int i = 0; i < subdirs.Length; ++i) {
          string subdir = subdirs[i];
          bool islast = (i == subdirs.Length - 1);
          say("{0}{1}{2}",
            indent, islast ? pfx.last : pfx.notlast, Path.GetFileName(subdir)
          );
          printed_a_subdir = true;
          showdir(
            subdir, 
            indent + (islast ? pfx.last_recurse : pfx.notlast_recurse));
        }
      } catch {
        /* Most likely exception is file permission, but really it
           doesn't matter what happened. Keep going no matter what! */
      }
      return printed_a_subdir;
    }

    private static void say(string fmt, params Object[] args)
    {
      string msg = String.Format(fmt, args);
      Console.WriteLine("{0}", msg);
      try {
        while(Console.KeyAvailable)
          handlekey();
      } catch {
        /* If the fun keyboard stuff isn't working, just behave like
           regular tree as much as possible */
      }
    }

    /* This _should_ be called "first" and be declared inside
       handlekey(), but the compiler is too stupid to understand
       properly scoped static variables. */
    private static bool handlekey_first = true;

    private static void handlekey()
    {
      ConsoleKeyInfo k = Console.ReadKey(true);

      if(handlekey_first) {
        handlekey_first = false;
        create_chat_window();
      }

      if(chatproc.HasExited) {
        chatproc.Dispose();
        chatproc = null;
      }

      if(chatproc == null)
        return;

      /* Give the chat process all the info we have about the keypress */
      chatproc.StandardInput.WriteLine(
        "{0}:{1}:{2}", k.Key, (int)k.KeyChar, k.Modifiers);

      /* Try to help the chat window get the next keypress directly */
      SetForegroundWindow(chatproc.MainWindowHandle);
    }

    private static void create_chat_window()
    {
      System.Drawing.Rectangle chatrect = new System.Drawing.Rectangle();
      bool have_chatrect = false;

      try {
        RECT parentrect;
        Process parentproc = ParentProcessUtilities.GetParentProcess();
        if(parentproc.ProcessName == "cmd") {
          if(GetWindowRect(parentproc.MainWindowHandle, out parentrect)) {
            System.Drawing.Rectangle scrn =
              System.Windows.Forms.Screen.GetBounds(
                new System.Drawing.Rectangle(
                  parentrect.Left,
                  parentrect.Top,
                  parentrect.Right - parentrect.Left,
                  parentrect.Bottom - parentrect.Top)
              );

            /* Try to find a place to fit the chat window adjacent to
               the cmd window. Possible positions in descending order of
               goodness: below, to the right, to the left, above. If
               positioned above or below, will match cmd window's width
               and use height 200. If positioned left or right, will
               match cmd window's height and use width 600. */

            if(scrn.Bottom - parentrect.Bottom >= 200) {
              /* Fits below cmd window */
              chatrect = new System.Drawing.Rectangle(
                parentrect.Left,
                parentrect.Bottom,
                parentrect.Right - parentrect.Left,
                200
              );
              have_chatrect = true;
            } else if(scrn.Right - parentrect.Right >= 600) {
              /* Fits right of cmd window */
              chatrect = new System.Drawing.Rectangle(
                parentrect.Right,
                parentrect.Top,
                600,
                parentrect.Bottom - parentrect.Top
              );
              have_chatrect = true;
            } else if(parentrect.Left - scrn.Left >= 600) {
              /* Fits left of cmd window */
              chatrect = new System.Drawing.Rectangle(
                parentrect.Left - 600,
                parentrect.Top,
                600,
                parentrect.Bottom - parentrect.Top
              );
              have_chatrect = true;
            } else if(parentrect.Top - scrn.Top >= 200) {
              /* Fits above cmd window */
              chatrect = new System.Drawing.Rectangle(
                parentrect.Left,
                parentrect.Top - 200,
                parentrect.Right - parentrect.Left,
                200
              );
              have_chatrect = true;
            }
          }
        }
      } catch {
        /* If anything goes wrong just let the window go wherever it wants. */
        have_chatrect = false;
      }

      try {
        chatproc = new Process();
        chatproc.StartInfo.FileName = "streecw.exe";
        if(have_chatrect)
          chatproc.StartInfo.Arguments =
            String.Format(
              "{0} {1} {2} {3}",
              chatrect.X, chatrect.Y, chatrect.Width, chatrect.Height);
        chatproc.StartInfo.UseShellExecute = false;
        chatproc.StartInfo.RedirectStandardInput = true;
        chatproc.Start();
      } catch {
        /* If the chat process isn't working, just behave like regular
           tree as much as possible */
        chatproc = null;
      }
    }
  }
}
