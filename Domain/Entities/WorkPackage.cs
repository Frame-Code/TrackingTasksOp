using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    //Representa todo tipo de tarea dentro de OpenProject
    public class WorkPackage
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string DueDate { get; set; } = string.Empty;
        public int? PercentageDone { get; set; }
    }
}
