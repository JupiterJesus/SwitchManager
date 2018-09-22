using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.cdn
{
    public class EshopLogin
    {
        public EshopLogin(string token, long expires)
        {
            Token = token;
            Expiration = DateTime.Now.AddSeconds(expires);
        }

        public string Token { get; set; }
        public DateTime Expiration { get; set; }
    }
}
