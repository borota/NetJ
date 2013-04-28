using System.Windows.Forms;

namespace J.Wd
{
    public static class FWd
    {
        private static Form mainWindow;
        private static Form MainWindow
        {
            get
            {
                if (null == mainWindow)
                {
                    mainWindow = new Form();
                    mainWindow.Text = Parser.mainWindowId;
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
            MainWindow = new Form();
            MainWindow.Text = Parser.mainWindowId;
        }

        internal static void ShowParent()
        {
            MainWindow.ShowDialog();
        }
    }
}
