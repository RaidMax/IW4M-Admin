using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

//DILEMMA -- Seperate intance of RCON for each server or no?
namespace IW4MAdmin
{
    class RCON
    {
        enum Type
        {
            Query,
            Execute,
        }

        public RCON(Server I)
        {
            sv_connection = new UdpClient();
            sv_connection.Client.SendTimeout = 1000;
            sv_connection.Client.ReceiveTimeout = 1000;
            Instance = I;
            toSend = new Queue<String>();
        }

        //When we don't care about a response
        public bool sendRCON(String message)
        {
            try
            {
                String STR_REQUEST = String.Format("ÿÿÿÿrcon {0} {1}", Instance.getPassword(), message);
                
                Byte[] Request_ = Encoding.Unicode.GetBytes(STR_REQUEST);
                Byte[] Request = new Byte[Request_.Length / 2];

                int count = 0; //This is kinda hacky but Unicode -> ASCII doesn't seem to be working correctly for this.
                foreach (Byte b in Request_)
                {
                    if (b != 0)
                        Request[count / 2] = b;
                    count++;
                }


                System.Net.IPAddress IP = System.Net.IPAddress.Parse(Instance.getIP());         
                IPEndPoint endPoint = new IPEndPoint(IP, Instance.getPort());

                sv_connection.Connect(endPoint);
                sv_connection.Send(Request, Request.Length);
            }

            catch (SocketException)
            {
                Instance.Log.Write("Unable to reach server for sending RCON", Log.Level.Debug);
                sv_connection.Close();
                return false;
            }

            return true;
        }
        //We want to read the reponse
        public String[] responseSendRCON(String message)
        {
            try
            {
                String STR_REQUEST;
                if (message != "getstatus")
                    STR_REQUEST = String.Format("ÿÿÿÿrcon {0} {1}", Instance.getPassword().Replace("\r", String.Empty), message);
                else
                    STR_REQUEST = String.Format("ÿÿÿÿ getstatus");

                Byte[] Request_ = Encoding.Unicode.GetBytes(STR_REQUEST);
                Byte[] Request = new Byte[Request_.Length/2];

                int count = 0; //This is kinda hacky but Unicode -> ASCII doesn't seem to be working correctly for this.
                foreach (Byte b in Request_)
                {
                    if (b != 0)
                        Request[count/2] = b;
                    count++;
                }


                System.Net.IPAddress IP = System.Net.IPAddress.Parse(Instance.getIP());
                IPEndPoint endPoint = new IPEndPoint(IP, Instance.getPort());

                sv_connection.Connect(endPoint);
                sv_connection.Send(Request, Request.Length);
            

                Byte[] receive = sv_connection.Receive(ref endPoint);
                int num = int.Parse("0a", System.Globalization.NumberStyles.AllowHexSpecifier);          

                String[] response = System.Text.Encoding.UTF8.GetString(receive).Split((char)num);

                if(response[1] == "Invalid password.")
                {
                    Instance.Log.Write("Invalid RCON password specified", Log.Level.Debug);
                    return null;
                }

                return response;
            }

            catch (SocketException)
            {
                Instance.Log.Write("Unable to reach server for sending RCON", Log.Level.Debug);
                sv_connection.Close();
                return null;
            }
        }

        public void addRCON(String Message, int delay)
        {
            toSend.Enqueue(Message);
        }

        public void ManageRCONQueue()
        {
            while (true)
            {
                if (toSend.Count > 0)
                {
                    sendRCON(toSend.Peek());
                    toSend.Dequeue();
                    Utilities.Wait(0.567);
                }
                else
                    Utilities.Wait(0.01);
            }
        }

        private UdpClient sv_connection;
        private Server Instance;
        private Queue<String> toSend;
    }
}
