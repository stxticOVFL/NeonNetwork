using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeonNetwork.Online
{
    internal static class Info
    {
        internal enum Opcodes
        {
            MetaSHandshake = 0x0100,
            MetaCHandshake = 0x8100,
            MetaSLogin = 0x0101,
            MetaCLogin = 0x8101,
            MetaPing = 0x81FF,
            MetaPong = 0x01FF,

            UserLookup = 0x8200,
            UserInfo = 0x0200,
            UserPFP = 0x0201,
            UserUpdate = 0x8210,
            UserSetPFP = 0x8211,
            UserSetPS = 0x8218,

            LBLookup = 0x8300,
            LBLookupSingle = 0x8308,
            LBInfo = 0x0300,
            LBAdd = 0x8310,
            LBAddBulk = 0x8318,
            LBDelete = 0x831F,

            ReplayLookup = 0x8400,
            ReplayInfo = 0x0400,
            ReplayAdd = 0x8410,
            ReplayDelete = 0x841F,

            RoomsLookup = 0x8500,
            RoomsInfo = 0x0500,
            RoomsAdd = 0x8510,
            RoomsUpdate = 0x8511,
            RoomsDelete = 0x851F,

            RoomsReady = 0x8520,
            RoomsSDataC = 0x8521,
            RoomsSDataS = 0x0521,
            RoomsJoin = 0x052D,
            RoomsKick = 0x852E,
            RoomsLeave = 0x852F,

            EventOnline = 0x0800,
            EventPB = 0x0810,
            EventWR = 0x0811,

            ErrorAllGood = 0x0F00,
            ErrorNoAuth = 0x0F10,
            ErrorMissing = 0x0F11,
            ErrorBanned = 0x0F12,
            ErrorPSReset = 0x0F13,
            ErrorProtocol = 0x0F20,
            ErrorNoOp = 0x0F21,
            ErrorBadData = 0x0F22,
            ErrorKicked = 0x0F30,
            ErrorStopped = 0x0F31,
            ErrorDeleted = 0x0F32,
            ErrorWHAT = 0x0FFF
        }
    }
}
