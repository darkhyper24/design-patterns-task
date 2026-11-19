using OrderEngine.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderEngine.Validation
{
    public interface IOrderServiceValidation
    {
        public void Validate(PlaceOrderRequest orderRequest);
    }
}
