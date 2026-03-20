using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SECS_emulator.Data
{
    public class StreamFunctionBase : SECSMessage
    {
        public SECSItem CreateBody(Int32 i)
        {
            List<SECSItem> items = new List<SECSItem>(i);
            SECSItem body = new SECSItem(DataType.LIST, items);
            return body;
        }

        public  SECSMessage Create_S1F14(SECSMessage msg, uint systemBytes)
        {
            byte[] HACK = new byte[1];
            HACK[0] = 0 ;

            msg.SetSFW(1, 4, 0);
            msg.Body = CreateBody(2);
            //msg.Body.Items[0] = new SECSItem(DataType.BINARY, HACK);
            msg.Body.Items.Add(new SECSItem(DataType.BINARY, HACK));
            //msg.Body.Items[1] = new SECSItem(DataType.LIST, new List<SECSItem>());
            msg.Body.Items.Add(new SECSItem(DataType.LIST, new List<SECSItem>()));
            
            return msg;
        }
    }
}
