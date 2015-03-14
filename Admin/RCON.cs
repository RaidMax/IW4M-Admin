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
            toSend = new Queue<RCON_Request>();
        }

        //We want to read the reponse
        public String[] responseSendRCON(String message)
        {
            try
            {
                String STR_REQUEST = String.Empty;
                if (message != "getstatus")
                    STR_REQUEST = String.Format("ÿÿÿÿrcon {0} {1}", Instance.getPassword(), message);
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

                String incoming = String.Empty;
                byte[] bufferRecv = new byte[65536];
                do
                {
                    // loop on receiving the bytes
                    bufferRecv = sv_connection.Receive(ref endPoint);

                    // only decode the bytes received
                    incoming += (Encoding.ASCII.GetString(bufferRecv, 0, bufferRecv.Length));
                } while (sv_connection.Available > 0);

                int num = int.Parse("0a", System.Globalization.NumberStyles.AllowHexSpecifier);

                String[] response = incoming.Split(new char[] {(char)num} , StringSplitOptions.RemoveEmptyEntries);

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
                return null;
            }

            catch (System.InvalidOperationException)
            {
                Instance.Log.Write("RCON Connection terminated by server. Uh-OH", Log.Level.Debug);
                sv_connection.Close();
                sv_connection = new UdpClient();
                return null;
            }
        }

        public String[] addRCON(String Message)
        {
            RCON_Request newReq = new RCON_Request(Message);
            toSend.Enqueue(newReq);
            return newReq.waitForResponse();
        }

        public void ManageRCONQueue()
        {
            while (Instance.isRunning)
            {
                if (toSend.Count > 0)
                {
                    RCON_Request Current = toSend.Peek();
                    Current.Response = responseSendRCON(Current.Request);
                    toSend.Dequeue();
                    Utilities.Wait(0.567);
                }
                else
                    Utilities.Wait(0.01);
            }
        }

        private UdpClient sv_connection;
        private Server Instance;
        private Queue<RCON_Request> toSend;
    }

    class RCON_Request
    {
        public RCON_Request(String IN)
        {
            Request = IN;
            Response = null;
        }

        public String[] waitForResponse()
        {
            DateTime Start = DateTime.Now;
            DateTime Current = DateTime.Now;
            while (Response == null && (Current-Start).TotalMilliseconds < 1000)
            {
                Thread.Sleep(1);
                Current = DateTime.Now;
            }
            return Response;
        }

        public String Request;
        public String[] Response;
    }

}
