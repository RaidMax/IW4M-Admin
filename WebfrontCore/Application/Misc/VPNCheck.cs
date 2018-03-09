using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Application.Misc
{
    public class VPNCheck
    {
        public static async Task<bool> UsingVPN(string ip)
        {
            try
            {
                using (var RequestClient = new System.Net.Http.HttpClient())
                {
                    string response = await RequestClient.GetStringAsync($"http://check.getipintel.net/check.php?ip={ip}&contact=raidmax@live.com");
                    double probability = Convert.ToDouble(response);
                    return probability > 0.9;
                }
            }

            catch (Exception)
            {
                return false;
            }
        }
    }
}
