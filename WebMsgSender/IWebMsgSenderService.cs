using InternalContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebMsgSender
{
    public interface IWebMsgSenderService
    {
        Task<string> SendMessage(JsonMessageData message, IClientConfiguration clientConfiguration, HttpMethod httpMethod);
        Task<string> SendMessage(XmlMessageData message, IClientConfiguration clientConfiguration, HttpMethod httpMethod);
        
    }
}
