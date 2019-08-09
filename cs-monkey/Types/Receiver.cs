using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_monkey.Types
{
    /// <summary>
    /// Defines a cryptshare user which should receive a file upload
    /// </summary>
    public class Receiver
    {
        public string Email { get; }
        public Receiver(string email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }
}
