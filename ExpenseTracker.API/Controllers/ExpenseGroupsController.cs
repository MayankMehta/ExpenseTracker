using ExpenseTracker.API.Helpers;
using ExpenseTracker.Repository;
using ExpenseTracker.Repository.Factories;
using Marvin.JsonPatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Routing;

namespace ExpenseTracker.API.Controllers
{
    //[EnableCors("http://www.contoso.com/, http://www.expensetracker.com/")] // Enabling cross-origin resource sharing. This means that cross-origin calls from these 2 domains will be allowed and other domains will be blocked by the browser.
    //[EnableCors("*", "*", "GET, POST")] // Because we do not know who our clients are going to be, we should use the wild-card character *
    public class ExpenseGroupsController : ApiController
    {
        IExpenseTrackerRepository _repository;
        ExpenseGroupFactory _expenseGroupFactory = new ExpenseGroupFactory();
        const int maxPageSize = 10;

        public ExpenseGroupsController()
        {
            _repository = new ExpenseTrackerEFRepository(new 
                Repository.Entities.ExpenseTrackerContext());
        }

        public ExpenseGroupsController(IExpenseTrackerRepository repository)
        {
            _repository = repository;
        }

        // Filtering: is filtering data based on clients. For instance:
        // - We want to show only the open ExpenseGroups
        // - We only want to see data of the logged in user
        // To achieve this, we pass in status and userId as parameters
        // To test pagination (with sorting and filtering), your URI should be: /api/expensegroups?page=2&pagesize=2
        // "fields" in the parameter list is used for data shaping. It is used when clients want only specific fields from the resource.
        [Route("api/expensegroups", Name = "ExpenseGroupsList")]
        public IHttpActionResult Get(string fields = null, string sort="id", string status = null, string userId = null
            , int page = 1, int pageSize = maxPageSize)
        {
            try
            {
                bool includeExpenses = false;
                List<string> lstOfFields = new List<string>();

                // we should include expenses when the fields-string contains "expenses", or "expenses.id", …
                if (fields != null)
                {
                    lstOfFields = fields.ToLower().Split(',').ToList();
                    includeExpenses = lstOfFields.Any(f => f.Contains("expenses"));
                }

                int statusId = -1;
                if(status != null)
                {
                    switch(status.ToLower())
                    {
                        case "open": statusId = 1;
                            break;
                        case "confirmed": statusId = 2;
                            break;
                        case "processed": statusId = 3;
                            break;
                        default:
                            break;
                    }
                }

                IQueryable<Repository.Entities.ExpenseGroup> expenseGroups = null;
                if (includeExpenses)
                {
                    // get expensegroups from the repository
                    expenseGroups = _repository.GetExpenseGroupsWithExpenses();
                }
                else
                {
                    expenseGroups = _repository.GetExpenseGroups();
                }

                expenseGroups = expenseGroups.ApplySort(sort)
                    .Where(eg => (statusId == -1 || eg.ExpenseGroupStatusId == statusId)) // Filtering: Only displaying open expensegroups
                    .Where(eg => (userId == null || eg.UserId == userId)); // Filtering: Only displaying data for the user who is logged in

                // ensure the page size isn't larger than the maximum.
                if (pageSize > maxPageSize)
                {
                    pageSize = maxPageSize;
                }

                // calculate data for metadata
                var totalCount = expenseGroups.Count();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var urlHelper = new UrlHelper(Request); // The UrlHelper allows us to easliy create a link from the route that's being used to access this action. We have to name the route which is why I named this route Name="ExpenseGroupsList"
                var prevLink = page > 1 ? urlHelper.Link("ExpenseGroupsList", // Link to previous page
                    new // We need to pass in all the parameters that are passed in to this method because all the pages will use these parameters to determine what gets displayed.
                    {
                        page = page - 1,
                        pageSize = pageSize,
                        sort = sort,
                        fields = fields,
                        status = status,
                        userId = userId
                    }) : "";
                var nextLink = page < totalPages ? urlHelper.Link("ExpenseGroupsList", // Link to next page
                    new
                    {
                        page = page + 1,
                        pageSize = pageSize,
                        sort = sort,
                        fields = fields,
                        status = status,
                        userId = userId
                    }) : "";

                var paginationHeader = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalCount = totalCount,
                    totalPages = totalPages,
                    previousPageLink = prevLink,
                    nextPageLink = nextLink
                };

                // Adding pagination information to Http "Header" collection
                HttpContext.Current.Response.Headers.Add("X-Pagination",
                   Newtonsoft.Json.JsonConvert.SerializeObject(paginationHeader));

                return Ok(expenseGroups
                    .Skip(pageSize * (page - 1))
                    .Take(pageSize)
                    .ToList()
                    .Select(eg => _expenseGroupFactory.CreateDataShapedObject(eg, lstOfFields))); // We want to send over the models from our DTO project. So we need to map these entities using the factory to their corresponding DTO models. This statement will return an expense DTO.

            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        public IHttpActionResult Get(int id, string fields = null)
        {
            try
            {
                bool includeExpenses = false;
                List<string> lstOfFields = new List<string>();

                // we should include expenses when the fields-string contains "expenses"
                if (fields != null)
                {
                    lstOfFields = fields.ToLower().Split(',').ToList();
                    includeExpenses = lstOfFields.Any(f => f.Contains("expenses"));
                }


                Repository.Entities.ExpenseGroup expenseGroup;
                if (includeExpenses)
                {
                    expenseGroup = _repository.GetExpenseGroupWithExpenses(id);
                }
                else
                {
                    expenseGroup = _repository.GetExpenseGroup(id);
                }

                if (expenseGroup != null)
                {
                    // We want to send over the models from our DTO project. So we need to map these entities using the factory to their corresponding DTO models. This statement will return an expense DTO.
                    return Ok(_expenseGroupFactory.CreateDataShapedObject(expenseGroup, lstOfFields));
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        [Route("api/expensegroups")]
        [HttpPost]
        // This should accept expense group as a parameter.
        // If you use FromBody attribute in the parameter list, it will automatically try and parse the request body to an expense group
        public IHttpActionResult Post([FromBody] DTO.ExpenseGroup expenseGroup)
        {
            try
            {
                if (expenseGroup == null)
                {
                    return BadRequest();
                }

                // try mapping & saving
                var eg = _expenseGroupFactory.CreateExpenseGroup(expenseGroup);

                var result = _repository.InsertExpenseGroup(eg);
                if (result.Status == RepositoryActionStatus.Created)
                {
                    // map to DTO
                    var newExpenseGroup = _expenseGroupFactory.CreateExpenseGroup(result.Entity);
                    return Created<DTO.ExpenseGroup>(Request.RequestUri
                        + "/" + newExpenseGroup.Id.ToString(), newExpenseGroup);
                }

                return BadRequest();
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        [HttpPut]
        // This should accept expense group as a parameter.
        // If you use FromBody attribute in the parameter list, it will automatically try and parse the request body to an expense group
        public IHttpActionResult Put(int id, [FromBody] DTO.ExpenseGroup expenseGroup)
        {
            try
            {
                if (expenseGroup == null)
                {
                    return BadRequest();
                }
                
                // map
                var eg = _expenseGroupFactory.CreateExpenseGroup(expenseGroup);

                var result = _repository.UpdateExpenseGroup(eg);
                if (result.Status == RepositoryActionStatus.Updated)
                {
                    // map to DTO
                    var updatedExpenseGroup = _expenseGroupFactory.CreateExpenseGroup(result.Entity);
                    return Ok(updatedExpenseGroup);
                }
                else if (result.Status == RepositoryActionStatus.NotFound)
                {
                    return NotFound();
                }

                return BadRequest();
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        [HttpPatch]
        // The [FromBody]JsonPatchDocument parameter indicates that from the body it is going to try and parse the JsonPatchDocument
        public IHttpActionResult Patch(int id, [FromBody]JsonPatchDocument<DTO.ExpenseGroup> expenseGroupPatchDocument)
        {
            try
            {
                if (expenseGroupPatchDocument == null)
                {
                    return BadRequest();
                }

                var expenseGroup = _repository.GetExpenseGroup(id);
                if (expenseGroup == null)
                {
                    return NotFound();
                }

                // map
                var eg = _expenseGroupFactory.CreateExpenseGroup(expenseGroup);

                // apply changes to the DTO
                expenseGroupPatchDocument.ApplyTo(eg);

                // map the DTO with applied changes to the entity & update
                var result = _repository.UpdateExpenseGroup(_expenseGroupFactory.CreateExpenseGroup(eg));

                if (result.Status == RepositoryActionStatus.Updated)
                {
                    // map to DTO
                    var patchedExpenseGroup = _expenseGroupFactory.CreateExpenseGroup(result.Entity);
                    return Ok(patchedExpenseGroup);
                }

                return BadRequest();
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var result = _repository.DeleteExpenseGroup(id);

                if (result.Status == RepositoryActionStatus.Deleted)
                {
                    return StatusCode(HttpStatusCode.NoContent);
                }
                else if (result.Status == RepositoryActionStatus.NotFound)
                {
                    return NotFound();
                }

                return BadRequest();
            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }
    }
}
