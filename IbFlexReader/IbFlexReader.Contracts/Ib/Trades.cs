﻿namespace IbFlexReader.Contracts.Ib
{
    using System.Collections.Generic;

    public class Trades
    {
        public List<Lot> Lot { get; set; }
        public List<Trade> Trade { get; set; }
    }
}