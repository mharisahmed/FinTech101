﻿using FinTech101.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace FinTech101.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Fintech()
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var companies = from p in aadc.Companies
                                where p.IsActive == true
                                && p.HasOnlyBasicProfile == false
                                && p.MarketStatusID == 3
                                orderby p.ShortNameEn
                                select new
                                {
                                    Value = p.CompanyID,
                                    Text = p.ShortNameEn + " - " + p.CompanyNameEn
                                };
                ViewBag.AllCompanies = companies.AsEnumerable().Select((item, index) => new SelectListItem() { Value = item.Value.ToString(), Text = item.Text }).ToList<SelectListItem>();

                var events = from p in aadc.GlobalEvents
                             select new
                             {
                                 Value = p.GlobalEventID,
                                 Desc = p.EventDesc,
                                 StartDate = p.StartsOn,
                                 EndDate = p.EndsOn
                             };
                ViewBag.AllEvents = events.AsEnumerable().Select((item, index) => new SelectListItem() { Value = item.Value.ToString(), Text = item.Desc /*+ " [" + item.StartDate.ToString("dd MMM yyyy") + "]"*/ }).ToList<SelectListItem>();
            }

            List<String> years = new List<string>();
            for (int i = 1993; i <= 2016; i++)
            {
                years.Add(i.ToString());
            }

            ViewBag.AllYearsSL = new SelectList(years.Select(year => new SelectListItem() { Value = year, Text = year }), "Value", "Text");

            return View();
        }

        public ActionResult q1(int companyID, string upOrDown, float percent, int fromYear, int toYear)
        {
            var result = FintechService.CompanyWasUpOrDownByPercent(companyID, upOrDown, percent, fromYear, toYear);

            ViewBag.result = result;

            ViewBag.years = (from p in result
                             group p by p.year into g
                             select new KeyAndCount
                             {
                                 Key = g.Key.ToString(),
                                 Count = g.Count()
                             }).ToList();

            return PartialView();
        }

        public ActionResult q2(int companyID, int year)
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var result = aadc.SP_CommpanyGoodAndBadDays(companyID, year);

                ViewBag.result = result.ToList();
            }

            return PartialView();
        }

        // Months company was up or down
        public ActionResult q3(int companyID, int from_year, int to_year, bool isPartial)
        {
            ViewBag.result = FintechService.MonthsCompanyWasUpOrDown(companyID, from_year, to_year);

            ViewBag.CompanyName = FintechService.GetCompany(companyID).CompanyNameEn;

            ViewBag.isPartial = isPartial;

            return PartialView();
        }
        public JsonResult q3_json(int companyID, int from_year, int to_year)
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var result = aadc.SP_MonthsCompanyWasUpOrDown(from_year, to_year, companyID);

                ViewBag.result = result.ToList();
            }

            return Json(ViewBag.result, JsonRequestBehavior.AllowGet);
        }

        // Which companies were up more than n percent in selected date range
        public ActionResult q5(int from_year, int to_year, decimal percent)
        {
            ViewBag.result = FintechService.CompaniesWhichWereUpMoreThanEnnPercentOfTheTime(from_year, to_year, percent);

            ViewBag.percent = percent;
            ViewBag.fromYear = from_year;
            ViewBag.toYear = to_year;

            return PartialView();
        }

        public ActionResult q4(int eventID, int weeksBefore, int weeksAfter, int companyID)
        {
            List<TableRowViewModel> model = null;

            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var eventDate = (from p in aadc.GlobalEvents
                                 where p.GlobalEventID == eventID
                                 select new
                                 {
                                     StartDate = p.StartsOn,
                                     EndDate = p.EndsOn
                                 }).FirstOrDefault();

                model = FintechService.GetCompanyPriceAroundDates_UI(eventDate.StartDate, eventDate.EndDate.HasValue ? eventDate.EndDate : null, weeksBefore, weeksAfter, companyID);
            }

            return (View("q4_with_range", model));
        }

        [NonAction]
        public ActionResult q4_old(int eventID, int weeksBefore, int weeksAfter, int companyID)
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var eventDate = (from p in aadc.GlobalEvents
                                 where p.GlobalEventID == eventID
                                 select new
                                 {
                                     StartDate = p.StartsOn,
                                     EndDate = p.EndsOn
                                 }).FirstOrDefault();

                var result = (aadc.SP_PricesAroundEvents(eventDate.StartDate, eventDate.EndDate, weeksBefore * -1, weeksAfter, companyID)).ToList();

                ViewBag.eventDate = eventDate;

                ViewBag.result = result;

                ViewBag.resultDates = (from r in result
                                       group r by new { r.ForDate, r.DoW }
                                      into grp
                                       select new DateWithDoW
                                       {
                                           ForDate = grp.Key.ForDate.Value,
                                           DoW = grp.Key.DoW
                                       }).ToList();

                ViewBag.CompanyID = companyID;
                ViewBag.CompanyName = (from p in aadc.Companies
                                       where p.CompanyID == companyID
                                       select p.CompanyNameEn).FirstOrDefault();

                ViewBag.eventDateClosingPriceWasZero = false;
                ViewBag.eventDateClosingPrice = (from r in result
                                                 where r.CID == companyID
                                                 select r.Close).Skip(weeksBefore * 7).Take(1).First();
                if (ViewBag.eventDateClosingPrice == 0)
                {
                    // Pick up the last closing value that was not 0
                    ViewBag.eventDateClosingPriceWasZero = true;
                    var a = (from r in result
                             where r.CID == companyID
                             && r.ForDate < eventDate.StartDate
                             && r.Close != 0
                             orderby r.ForDate descending
                             select new
                             {
                                 r.ForDate,
                                 r.Close
                             }).Take(1).First();

                    ViewBag.eventDateClosingPrice = a.Close;
                    ViewBag.eventDateClosingPriceAltDate = a.ForDate;
                }

                var b = (from r in result
                         where r.CID == companyID
                         && r.Close != 0
                         orderby r.ForDate
                         select new
                         {
                             r.ForDate,
                             r.Close
                         }).Take(1).First();
                ViewBag.firstValidClosingPrice = b.Close;
                ViewBag.firstValidClosingPriceDate = b.ForDate;

                var c = (from r in result
                         where r.CID == companyID
                         && r.Close != 0
                         orderby r.ForDate descending
                         select new
                         {
                             r.ForDate,
                             r.Close
                         }).Take(1).First();
                ViewBag.lastValidClosingPrice = c.Close;
                ViewBag.lastValidClosingPriceDate = c.ForDate;

                ViewBag.minClose = (from r in result
                                    where r.CID == companyID
                                    && r.Close > 0
                                    select r.Close).Min().Value;
                ViewBag.maxClose = (from r in result
                                    where r.CID == companyID
                                    select r.Close).Max().Value;

                ViewBag.weeksBefore = weeksBefore;
                ViewBag.weeksAfter = weeksAfter;

            }

            return (PartialView());
        }

        public ActionResult ListEvents()
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var events = (from p in aadc.GlobalEvents
                                  //where (eventCategoryID > 0 && p.EventCategoryID == eventCategoryID) || true
                              orderby p.StartsOn
                              select p).ToList();

                ViewBag.allEvents = events;

                var eventCategories = (from p in aadc.GlobalEventCategories
                                       select p).ToList();

                var allEventCategories = eventCategories.AsEnumerable().Select((item, index) => new SelectListItem() { Value = item.EventCategoryID.ToString(), Text = item.EventCategoryName }).ToList<SelectListItem>();
                allEventCategories.Insert(0, new SelectListItem() { Value = "0", Text = "SHOW ALL" });

                ViewBag.allEventCategories = allEventCategories;
            }

            return (View());
        }

        public ActionResult EventsList(int eventCategoryID = 0)
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var events = (from p in aadc.GlobalEvents
                              where (eventCategoryID > 0 && p.EventCategoryID == eventCategoryID) || (eventCategoryID == 0)
                              orderby p.StartsOn
                              select p).ToList();

                ViewBag.allEvents = events;
            }

            return (PartialView("_eventsList"));
        }

        public ActionResult AddEvent()
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                var eventCategories = (from p in aadc.GlobalEventCategories
                                       select p).ToList();

                var allEventCategories = eventCategories.AsEnumerable().Select((item, index) => new SelectListItem() { Value = item.EventCategoryID.ToString(), Text = item.EventCategoryName }).ToList<SelectListItem>();

                ViewBag.allEventCategories = allEventCategories;
            }

            return (View());
        }

        [HttpPost]
        public ActionResult AddEvent(GlobalEvent globalEvent)
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                aadc.GlobalEvents.InsertOnSubmit(globalEvent);
                aadc.SubmitChanges();
            }

            return (Redirect("/home/listevents"));
        }

        public JsonResult DeleteEvent(int eventID)
        {
            using (ArgaamAnalyticsDataContext aadc = new ArgaamAnalyticsDataContext())
            {
                //var ge = (from rec in aadc.GlobalEvents
                //          where rec.GlobalEventID == eventID
                //          select rec).Single();

                var ge = new GlobalEvent() { GlobalEventID = eventID };

                aadc.GlobalEvents.Attach(ge);
                aadc.GlobalEvents.DeleteOnSubmit(ge);
                aadc.SubmitChanges();
            }

            return (Json("SUCCESS", JsonRequestBehavior.AllowGet));
        }

        public ActionResult AddGlobalEvent()
        {
            return (View());
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}