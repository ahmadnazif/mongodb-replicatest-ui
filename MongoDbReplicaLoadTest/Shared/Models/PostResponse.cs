using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDbReplicaLoadTest.Shared.Models;
public class PostResponse
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
}
