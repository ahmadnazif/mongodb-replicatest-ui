using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDbReplicaLoadTest.Shared.Models;
public class InsertMultiSms : SmsBase
{
    public int Iteration { get; set; }
}
