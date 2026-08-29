using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Re4QuadExtremeEditor.src.Forms;

namespace Re4QuadExtremeEditor.src
{
    public static class SplashScreen
    {
        public static SplashScreenConteiner Conteiner { get; set; }

        /// <summary>
        /// True when the native splash window could not be created in this
        /// environment (e.g. wine/mono, X11, restricted window stations).
        /// The splash is purely cosmetic, so the app keeps running without it.
        /// </summary>
        public static bool SplashWindowFailed { get; private set; }

        private static void SplashScreenShow()
        {
            try
            {
                Application.Run(new SplashScreenForm(Conteiner));
            }
            catch (Exception ex)
            {
                SplashWindowFailed = true;
                try
                {
                    if (Conteiner != null)
                    {
                        Conteiner.FormIsClosed = true;
                        Conteiner.Close = null;
                        Conteiner.ReleasedToClose = null;
                    }
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Re4Quad_error_log.txt");
                    System.IO.File.AppendAllText(logPath,
                        "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] "
                        + "Splash window could not be created; continuing without it (compatibility fallback).\r\n"
                        + ex.GetType().Name + ": " + ex.Message + "\r\n\r\n");
                }
                catch { }
            }
        }

        public static void StartSplashScreen()
        {
            Conteiner = new SplashScreenConteiner();
            System.Threading.Thread threadSplashScreen = new System.Threading.Thread(SplashScreenShow);
            threadSplashScreen.SetApartmentState(System.Threading.ApartmentState.STA);
            threadSplashScreen.Start();
        }
    }

    public class SplashScreenConteiner
    {
        public Action Close { get; set; }
        public Action ReleasedToClose { get; set; }
        public bool FormIsClosed { get; set; }

        public SplashScreenConteiner() 
        {
            FormIsClosed = false;
        }
    }
}
