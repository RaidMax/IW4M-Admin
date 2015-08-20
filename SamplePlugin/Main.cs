using System;
using SharedLibrary;
using System.Text;

namespace SamplePlugin
{
    public class SampleCommand : Command
    {
        public SampleCommand() : base("testplugin", "sample plugin command. syntax !testplugin", "tp", Player.Permission.User, 0, false) { }

        public override void Execute(Event E)
        {
            Player clientWhoSent = E.Origin;
            Server originatingServer = E.Owner;

            String[] messageToClient = { 
                                           String.Format("The command {0} was requested by ^3{1}", Name, clientWhoSent.Name), 
                                           String.Format("The command was request on server ^1{0}", originatingServer.getName()) 
                                       };

            foreach (String Line in messageToClient)
                clientWhoSent.Tell(Line);
        }
    }

    public class AnotherSampleCommand : Command
    {
        public AnotherSampleCommand() : base("scream", "broadcast your message. syntax !scream <message>", "s", Player.Permission.Moderator, 1, false) { }

        public override void Execute(Event E)
        {
            Server originatingServer = E.Owner;
            String Message = E.Data;
            String Sender = E.Origin.Name;

            for (int i = 0; i < 10; i++)
                originatingServer.Broadcast(String.Format("^7{0}: {1}", Sender, Message));
        }
    }

    public class InvalidCommandExample
    {
        private void doNotDoThis() { }
    }
}