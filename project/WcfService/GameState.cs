using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WcfService
{
    [DataContract]
    class GameState
    {
        public enum GAME_STATE { WAIT_FIRST_PLAYER, WAIT_SECOND_PLAYER, FIRST_PLAYER, SECOND_PLAYER, END_GAME };
        public enum FIELD_STATE { FREE, CROSS, ZERO };

        [DataMember]
        public GAME_STATE state;
        [DataMember]
        public string firstPlayer;
        [DataMember]
        public string secondPlayer;
        [DataMember]
        public string currentPlayer;
        [DataMember]
        public FIELD_STATE[] field;

        public GameState()
        {
            field = new FIELD_STATE[9];
            state = GAME_STATE.WAIT_FIRST_PLAYER;
        }
    }
}
