﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDbReplicaLoadTest.Shared.Models;
public class SmsBase
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Content { get; set; }
}
