using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Application.Misc
{
    public class VPNCheck
    {
        public static async Task<bool> UsingVPN(string ip, string apiKey)
        {
#if DEBUG
            return await Task.FromResult(false);

#else
            try
            {
                using (var RequestClient = new System.Net.Http.HttpClient())
                {
                    RequestClient.DefaultRequestHeaders.Add("X-Key", apiKey);
                    string response = await RequestClient.GetStringAsync($"http://v2.api.iphub.info/ip/{ip}");
                    var responseJson = JsonConvert.DeserializeObject<JObject>(response);
                    int blockType = Convert.ToInt32(responseJson["block"]);
                    return blockType == 1;
                }
            }

            catch (Exception)
            {
                return false;
            }
#endif
        }
    }
}
