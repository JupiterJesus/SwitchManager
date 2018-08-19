using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// I don't know much about this. There is a file called control.nacp packed into the CONTROL NCA.
    /// It seems to contain metadata like language, version, title id, etc. It is alongside the game's icons,
    /// one for each supported language. The icon languages match the languages within the nacp file.
    /// I have seen some NSPs contain a file that ends in .nacp.xml. I have yet to read one of these files and compare
    /// it to the contents of the control.nacp file.
    /// 
    /// See http://switchbrew.org/index.php?title=Control.nacp.
    /// </summary>
    class NACP
    {
        public int NumLanguages { get; } = 0x10;

        public string[] ApplicationNames { get; private set; }
        public string[] DeveloperNames { get; private set; }

        public NACP()
        {
            ApplicationNames = new string[NumLanguages];
            DeveloperNames = new string[NumLanguages];
        }

        public NACP(string file) : this()
        {
            Parse(file);
        }

        public void Parse(string file)
        {
            FileStream fs = File.OpenRead(file);
            BinaryReader br = new BinaryReader(fs);

            // Reading CNMT file
            // See http://switchbrew.org/index.php?title=Control.nacp

            // 0x0 + 0x3000 (0x10 entries of 0x300 each) - language entries for developer name and title name
            for (int i = 0; i < NumLanguages; i++)
            {
                byte[] nameBytes = br.ReadBytes(0x200);
                byte[] devBytes = br.ReadBytes(0x100);
                ApplicationNames[i] = Encoding.UTF8.GetString(nameBytes);
                DeveloperNames[i] = Encoding.UTF8.GetString(devBytes);
            }

            // TODO finish parsing
            br.Close();
        }
    }
}
