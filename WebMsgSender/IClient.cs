using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebMsgSender
{
    public interface IClient
    {
        public HttpClient GetHttpClient();
    }
}
