using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace SwitchManager.nx.img
{

    public class SwitchImageLoader
    {
        // {0} = n
        // {1} = env
        // {2} = tid
        // {3} = ver
        // {4} = did
        private static string remotePathPattern = "https://atum{0}.hac.{1}.d4c.nintendo.net/t/a/{2}/{3}?device_id={4}";

        // {0} = tid
        private static string localPath = "Images";
        private static string localPathPattern = localPath + Path.DirectorySeparatorChar + "{0}.jpg";


        /// <summary>
        /// Loads a remote image from nintendo.
        /// 
        /// This is way more complicated and I know I'm gonna need more arguments passed in.
        /// Not implemented for now.
        /// </summary>
        /// <param name="titleID"></param>
        /// <returns></returns>
        public SwitchImage GetRemoteImage(string titleID)
        {
            return new SwitchImage("Images\\blank.jpg");
        }

        public SwitchImage GetLocalImage(string titleID)
        {
            if (Directory.Exists(localPath))
            {
                string location = string.Format(localPathPattern, titleID);
                if (File.Exists(location))
                {
                    SwitchImage img = new SwitchImage(location);
                    return img;
                }
            }
            else
            {
                Directory.CreateDirectory(localPath);
            }

            return null;
        }
    }
}