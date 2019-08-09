using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_monkey.Types
{
    public class Sender
    {
        public string Name { get; }

        public string Phone { get; }

        public string Email { get; }

        public Sender(string name, string phone, string email)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Phone = phone ?? throw new ArgumentNullException(nameof(phone));
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }
    }
}
