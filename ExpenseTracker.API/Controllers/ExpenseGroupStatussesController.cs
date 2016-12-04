using ExpenseTracker.Repository;
using ExpenseTracker.Repository.Factories;
using System;
using System.Linq;
using System.Web.Http;

namespace ExpenseTracker.API.Controllers
{
    //  [RoutePrefix("api/expensegroupstatusses")]
    public class ExpenseGroupStatussesController : ApiController
    {
        IExpenseTrackerRepository _repository;
        ExpenseMasterDataFactory _expenseMasterDataFactory = new ExpenseMasterDataFactory();

        public ExpenseGroupStatussesController()
        {
            _repository = new ExpenseTrackerEFRepository(new Repository.Entities.ExpenseTrackerContext());
        }

        public ExpenseGroupStatussesController(IExpenseTrackerRepository repository)
        {
            _repository = repository;
        }

        public IHttpActionResult Get()
        {
            try
            {
                // get expensegroupstatusses & map to DTOs
                var expenseGroupStatusses = _repository.GetExpenseGroupStatusses().ToList()
                    .Select(egs => _expenseMasterDataFactory.CreateExpenseGroupStatus(egs));

                return Ok(expenseGroupStatusses);
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }
    }
}