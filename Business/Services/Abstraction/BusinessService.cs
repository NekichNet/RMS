using Business.Models;

namespace Business.Services.Abstraction
{
    public abstract class BusinessService
    {
        public Unit? Actor { get; set; }
    }
}
