using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Users.Contracts
{
    public enum AddressType1
    {
        User,
        Child,
        Billing
    }

    public enum TranscationState1
    {
        Submitted,
        Processed,
        InProgress,
        Successful,
        Failed,// Auto failed from receving bank
        Cancelled,//Manually stopping
    }
}
