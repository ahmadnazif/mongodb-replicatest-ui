using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDbReplicaLoadTest.Shared.Models;
public class Sms : SmsBase
{
    [BsonId]
    public string? MsgId { get; set; }
    public DateTime InTime { get; set; }
}
