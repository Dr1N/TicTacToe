using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WcfService
{
    [ServiceContract]
    interface TicTacService
    {
        [OperationContract]
        bool Login(string login);

        [OperationContract]
        GameState GetState(string login);

        [OperationContract]
        void Action(string user, Point coord);

        [OperationContract]
        void ExitPlayer(string user);
    }
}
