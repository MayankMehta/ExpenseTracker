using ExpenseTracker.DTO;
using ExpenseTracker.WebClient.Helpers;
using ExpenseTracker.WebClient.Models;
using Marvin.JsonPatch;
using Newtonsoft.Json;
using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ExpenseTracker.WebClient.Controllers
{
    public class ExpenseGroupsController : Controller
    {

        public async Task<ActionResult> Index(int? page = 1)
        {
            var client = ExpenseTrackerHttpClient.GetClient();

            var model = new ExpenseGroupsViewModel();

            // Calling the GET method on API
            HttpResponseMessage egsResponse = await client.GetAsync("api/expensegroupstatusses");

            if (egsResponse.IsSuccessStatusCode)
            {
                string egsContent = await egsResponse.Content.ReadAsStringAsync();
                var lstExpenseGroupStatusses = JsonConvert
                    .DeserializeObject<IEnumerable<ExpenseGroupStatus>>(egsContent);

                model.ExpenseGroupStatusses = lstExpenseGroupStatusses;
            }
            else
            {
                return Content("An error occurred.");
            }

            // Without sorting
            //HttpResponseMessage response = await client.GetAsync("api/expensegroups");

            // With sorting. Expense groups will be sorted first by status code and then by title
            //HttpResponseMessage response = await client.GetAsync("api/expensegroups?sort=expensegroupstatusid,title");

            // With paging. We already have suport for paging in API but we need to add new support in the client.
            // I added PagingInfo.cs & HeaderParser.cs helper classes to implement paging on this MVC client.
            HttpResponseMessage response = await client.GetAsync("api/expensegroups?sort=expensegroupstatusid,title&page=" + page + "&pagesize=5");

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                var pagingInfo = HeaderParser.FindAndParsePagingInfo(response.Headers); // Also, added NuGet package pagedlist.mvc to implement paging
                var lstExpenseGroups = JsonConvert.DeserializeObject<IEnumerable<ExpenseGroup>>(content);

                var pagedExpenseGroupsList = new StaticPagedList<ExpenseGroup>(lstExpenseGroups,
                    pagingInfo.CurrentPage,
                    pagingInfo.PageSize,
                    pagingInfo.TotalCount);

                model.ExpenseGroups = pagedExpenseGroupsList;
                model.PagingInfo = pagingInfo;
            }
            else
            {
                return Content("An error occurred.");
            }

            return View(model);
        }

 
        // GET: ExpenseGroups/Details/5
        public async Task<ActionResult> Details(int id)
        {
            var client = ExpenseTrackerHttpClient.GetClient();

            // Data shaping - we are asking for specific fields and we are asking for expenses (which is an example of associations)
            HttpResponseMessage response = await client.GetAsync("api/expensegroups/" + id
            + "?fields=id,description,title,expenses");

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                var model = JsonConvert.DeserializeObject<ExpenseGroup>(content);
                return View(model);
            }

            return Content("An error occurred");
        }

        // GET: ExpenseGroups/Create
        // Clicking the Create New link on the page will bring you to this method. This method 
        // returns back the Create.cshtml view. Clicking the Create button on Create.cshtml 
        // will trigger POST and call the Create method below.
        public ActionResult Create()
        {
            return View();
        }

        // POST: ExpenseGroups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ExpenseGroup expenseGroup)
        {
            try
            {
                var client = ExpenseTrackerHttpClient.GetClient();

                // an expensegroup is created with status "Open", for the current user
                expenseGroup.ExpenseGroupStatusId = 1;
                expenseGroup.UserId = @"https://expensetrackeridsrv3/embedded_1";

                var serializedItemToCreate = JsonConvert.SerializeObject(expenseGroup);

                var response = await client.PostAsync("api/expensegroups",
                    new StringContent(serializedItemToCreate,
                        System.Text.Encoding.Unicode,
                        "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    // If everything went good we want to redirect to the list of ExpenseGroups
                    return RedirectToAction("Index");
                }
                else
                {
                    return Content("An error occurred.");
                }
            }
            catch
            {
                return Content("An error occurred.");
            }
        }

        // GET: ExpenseGroups/Edit/5
        // Clicking the Edit link for any expense group will bring you to this method. This method 
        // returns back the Edit.cshtml view. Clicking the Save button on Edit.cshtml 
        // will trigger POST and call the Edit method below.
        public async Task<ActionResult> Edit(int id)
        {
            var client = ExpenseTrackerHttpClient.GetClient();

            HttpResponseMessage response = await client.GetAsync("api/expensegroups/" + id
                + "?fields=id,title,description"); // Data shaping - requesting only specific fields from the API
            string content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {    
                var model = JsonConvert.DeserializeObject<ExpenseGroup>(content);
                return View(model);
            }

            return Content("An error occurred:" + content);
        }

        // POST: ExpenseGroups/Edit/5   
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int id, ExpenseGroup expenseGroup)
        {
            try
            {
                var client = ExpenseTrackerHttpClient.GetClient();

                JsonPatchDocument<DTO.ExpenseGroup> patchDoc = new JsonPatchDocument<ExpenseGroup>();
                patchDoc.Replace(eg => eg.Title, expenseGroup.Title);
                patchDoc.Replace(eg => eg.Description, expenseGroup.Description);

                var serializedItemToUpdate = JsonConvert.SerializeObject(patchDoc);

                // .NET Web APi does not provide any implementation for PatchAsync. As a result, I created my own PatchAsync helper method
                var response = await client.PatchAsync("api/expensegroups/" + id,
                    new StringContent(serializedItemToUpdate, System.Text.Encoding.Unicode, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    return Content("An error occurred");
                }
            }
            catch
            {
                return Content("An error occurred");
            }
        }

        // POST: ExpenseGroups/Delete/5
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var client = ExpenseTrackerHttpClient.GetClient();

                var response = await client.DeleteAsync("api/expensegroups/" + id);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    return Content("An error occurred");
                }
            }
            catch
            {
                return Content("An error occurred");
            }
        }
    }
}
