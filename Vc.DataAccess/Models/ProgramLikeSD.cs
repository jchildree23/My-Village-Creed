using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Vc.DataAccess.Models
{
    [MappedTable(Name = "programlikes")]
    public class ProgramLikeSD : BaseSD
    {
        [DisplayName("ID")]
        [MappedColumn(Name = "id", IsPrimaryKey = true)]
        public string Id { get; set; }

        [DisplayName("User ID")]
        [MappedColumn(Name = "userid", IsNullable = true, IsForeignKey = true)]
        public string UserId { get; set; }

        [DisplayName("Program ID")]
        [MappedColumn(Name = "programid", IsNullable = true)]
        public string ProgramId { get; set; }

        [DisplayName("Display Name")]
        [MappedColumn(Name = "displayname", IsNullable = true)]
        public string DisplayName { get; set; }
    }
}
