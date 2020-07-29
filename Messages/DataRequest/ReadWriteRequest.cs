using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Messages.DataRequest
{
    [Serializable]
    public abstract class ReadWriteRequest
    {
        #region Constructors

        public ReadWriteRequest()
        {
        }

        #endregion

        #region Properties

        public object ResponseValues { get; set; }

        #endregion
    }
}