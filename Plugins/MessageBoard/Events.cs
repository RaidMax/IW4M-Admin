using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageBoard.Events
{
    public delegate void ActionEventHandler(User origin, EventArgs e);
}
