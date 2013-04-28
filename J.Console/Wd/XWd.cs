using System.Windows;

namespace J.Wd
{
    public static class XWd
    {
        private static JWindow mainWindow;
        private static JWindow MainWindow
        {
            get
            {
                if (null == mainWindow)
                {
                    mainWindow = new JWindow(Parser.mainWindowId);
                }
                return mainWindow;
            }
            set
            {
                mainWindow = value;
            }
        }

        internal static void CreateParent(string windowId)
        {
            MainWindow = new JWindow(windowId);
        }

        internal static void ShowParent()
        {
            MainWindow.ShowDialog();
        }

        internal class JWindow : Window
        {
            public string Id;
            public JWindow(string id)
            {
                this.Id = id;
                this.Title = this.Id;
            }
        }
    }
}