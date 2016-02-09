using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;

using Vc.DataAccess;
using Vc.DataAccess.Models;
using Vc.DataAccess.Providers;

namespace Vc.Web.Controllers
{
    public class ProgramsController : BaseController
    {
        #region Constructor
        
        public ProgramsController()
        {
            DefaultRedirect = RedirectToAction("Index", "Dashboard");
        }

        #endregion

        [RequireSuperAdmin]
        public ActionResult Index(string Search, string Status, string Type, string Tags, string Category, DateTime? FromDate, DateTime? ToDate)
        {
            using (var provider = new ProgramProvider())
            {
                var programs = provider.Filter(Search, Status, Type, Tags, Category, FromDate, ToDate);

                return View(programs.Select(x => x.Program).OrderBy(x => x.Name));
            }
        }

        #region Program Viewing / Joining

        public new ActionResult View(string id)
        {
            using (var provider = new ProgramProvider())
            {
                var program = provider.GetProgram(id, true);

                if(program.Program.BlockedBy17)
                {
                    if (SessionVariables.CurrentUser != null && SessionVariables.CurrentUser.Age < 17)
                    {
                        return View("Blocked", program);
                    }

                    return View(program);
                }
                else
                {
                    return View(program);
                }
            }
        }

        public ActionResult Blocked()
        {
            return View();
        }

        //[HttpGet]
        //[RequireUser]
        //public ActionResult _Join(string id)
        //{
        //    using (var programProvider = new ProgramProvider())
        //    {
        //        var program = programProvider.GetProgram(id);

        //        return PartialView("_Join", program);
        //    }
        //}

        [HttpPost]
        [RequireUser]
        public ActionResult _JoinWithFinePrint(string id, string userType, bool accepted)
        {
            if (accepted)
            {
                if (!string.IsNullOrEmpty(userType))
                {
                    using (var programUserProvider = new ProgramUserProvider())
                    {
                        var programUsers = programUserProvider.GetForProgram(id, ProgramUserTypes.Participant);

                        if (!programUsers.Any(x => x.UserId.Equals(SessionVariables.CurrentUser.Id)))
                        {
                            var programUser = new ProgramUserSD
                            {
                                Id = DataAccess.Utilities.GenerateUniqueID(),
                                DateCreated = DateTime.Now,
                                ProgramId = id,
                                UserId = SessionVariables.CurrentUser.Id,
                                UserType = userType,
                                Status = ProgramUserStatus.Active
                            };

                            programUserProvider.Insert(programUser);

                            AlertMessage = "<strong>Congratulations!</strong> You have successfully joined this program.  If you have any questions, please contact the program administrator or sponsor. Share the news with your friends! <div class='fb-share-button' data-href='" + ApplicationCache.Instance.SiteUrl + "/Site/ViewProgram/" + id + "' style='vertical-align: middle;'></div>";
                            AlertMessageType = AlertMessageTypes.Success;
                        }
                        else
                        {
                            AlertMessage = "<strong>Uh oh!</strong> It looks like you are already a participant of this program.";
                            AlertMessageType = AlertMessageTypes.Failure;
                        }

                        return RedirectTo("/Site/ViewProgram/" + id);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Please select if you are a volunteer or participant");
                }
            }
            else
            {
                ModelState.AddModelError("", "Please agree to the terms");
            }

            using (var provider = new ProgramProvider())
            {
                var program = provider.GetProgram(id);

                return PartialView("_Join", program);
            }
        }

        [HttpPost]
        [RequireUser]
        public ActionResult _JoinWithoutFinePrint(string id, string userType)
        {
            if (!string.IsNullOrEmpty(userType))
            {
                using (var provider = new ProgramUserProvider())
                {
                    var programUsers = provider.GetForProgram(id, ProgramUserTypes.Participant);

                    if (!programUsers.Any(x => x.UserId.Equals(SessionVariables.CurrentUser.Id)))
                    {
                        var programUser = new ProgramUserSD
                        {
                            Id = DataAccess.Utilities.GenerateUniqueID(),
                            DateCreated = DateTime.Now,
                            ProgramId = id,
                            UserId = SessionVariables.CurrentUser.Id,
                            UserType = userType,
                            Status = ProgramUserStatus.Active
                        };

                        provider.Insert(programUser);

                        AlertMessage = "<strong>Congratulations!</strong> You have successfully joined this program.  If you have any questions, please contact the program administrator or sponsor. Share the news with your friends! <div class='fb-share-button' data-href='" + ApplicationCache.Instance.SiteUrl + "/Site/ViewProgram/" + id + "' style='vertical-align: middle;'></div>";
                        AlertMessageType = AlertMessageTypes.Success;
                    }
                    else
                    {
                        AlertMessage = "<strong>Uh oh!</strong> It looks like you are already a participant of this program.";
                        AlertMessageType = AlertMessageTypes.Failure;
                    }

                    return RedirectTo("/Site/ViewProgram/" + id);
                }
            }

            ModelState.AddModelError("", "Please select if you are a participant or volunteer");

            using (var provider = new ProgramProvider())
            {
                var program = provider.GetProgram(id);

                return PartialView("_Join", program);
            }
        }

        [RequireUser]
        public ActionResult Leave(string programUserId, string programId)
        {
            using (var provider = new ProgramUserProvider())
            {
                provider.Delete(programUserId);

                AlertMessage = "<strong>Sorry to see you go!</strong> You have successfully left this program.  If you have any questions, please contact the program administrator or sponsor.";
                AlertMessageType = AlertMessageTypes.Failure;

                return Redirect("/Site/ViewProgram/" + programId);
            }
        }

        [RequireSuperAdmin]
        public ActionResult Admin(string status, string locationType)
        {
            ViewBag.LocationType = locationType;
            ViewBag.Status = status;

            using (var provider = new ProgramProvider())
            {
                var programs = provider.GetForAdmin(status, locationType);

                return View(programs);
            }
        }

        [RequireUser]
        public void Like(string id)
        {
            using (var provider = new ProgramLikeProvider())
            {
                var currentLike = provider.GetLikeForProgram(id, SessionVariables.CurrentUser.Id);

                if (currentLike == null)
                {
                    var like = new ProgramLikeSD
                    {
                        ProgramId = id, 
                        UserId = SessionVariables.CurrentUser.Id,
                        DisplayName = SessionVariables.CurrentUser.DisplayName
                    };

                    provider.Insert(like);
                }
                else
                {
                    provider.Delete(currentLike.Id);
                }
            }
        }

        #endregion

        #region Create / Edit Program

        [HttpGet]
        [RequireUser]
        public ActionResult Create(string sponsorId)
        {
            var sponsor = !string.IsNullOrEmpty(sponsorId) ? ApplicationCache.Instance.Sponsors.FirstOrDefault(x => x.Id.Equals(sponsorId)) : null;
            var organization = sponsor != null ? sponsor.Name : "";
            var website = sponsor != null ? sponsor.Website : "";

            var item = new ProgramViewSD() { New = true, Program = new ProgramSD
            {
                Id = DataAccess.Utilities.GenerateUniqueID(), 
                SponsorId = sponsorId, 
                CreatedBy = SessionVariables.CurrentUser.Id, 
                DoesCost = false, 
                Visibility = ProgramVisibility.Public, 
                Status = ProgramStatus.Draft, 
                LocationType = ProgramLocationTypes.Items.First(), 
                SponsoringOrganization = organization, 
                SponsoringOrganizationWebsite = website
            } };

            return View(DefaultViews.CreateEdit, item);
        }

        [HttpPost]
        [RequireUser]
        public ActionResult Create(ProgramViewSD item)
        {
            if (ModelState.IsValid)
            {
                if (item.ImageFile != null && item.ImageFile.ContentLength > 0)
                {
                    var fileName = DataAccess.Utilities.GenerateUniqueID() + Path.GetExtension(item.ImageFile.FileName);

                    try
                    {
                        var result = AwsHelpers.UploadImage(fileName, item.ImageFile);

                        item.Program.Image = fileName;
                    }
                    catch (Exception ex)
                    {
                        AlertMessage = "Your program has been saved successfully, but there was a problem uploading your image.  Please contact our support if you continue to have problems.";
                    }
                }

                using (var provider = new ProgramProvider())
                {
                    if (string.IsNullOrEmpty(item.Program.StartDate))
                    {
                        item.Program.StartDate = DateTime.Now.ToShortDateString();
                    }
                    if (string.IsNullOrEmpty(item.Program.EventStartTime))
                    {
                        item.Program.EventStartTime = "12:00 AM";
                    }
                    if (string.IsNullOrEmpty(item.Program.EventStopTime))
                    {
                        item.Program.EventStopTime = "12:00 AM";
                    }

                    provider.Insert(item.Program);
                }

                return Redirect("/Site/ViewProgram/" + item.Program.Id);
            }

            return View(DefaultViews.CreateEdit, item);
        }

        [HttpGet]
        [RequireUser]
        public ActionResult Edit(string id)
        {
            using (var provider = new ProgramProvider())
            {
                var item = provider.GetProgram(id);

                if (!item.ProgramUsers.Where(x => !string.IsNullOrEmpty(x.UserId)).Any(x => x.UserType.Equals(ProgramUserTypes.Administrator) && x.UserId.Equals(SessionVariables.CurrentUser.Id)) && !SessionVariables.CurrentUser.SuperAdmin)
                {
                    return RedirectToAction("AccessDenied", "Error");
                }

                return View(DefaultViews.CreateEdit, item);
            }
        }

        [HttpPost]
        [RequireUser]
        public ActionResult Edit(ProgramViewSD item)
        {
            if (ModelState.IsValid)
            {
                if (item.ImageFile != null && item.ImageFile.ContentLength > 0)
                {
                    var fileName = DataAccess.Utilities.GenerateUniqueID() + Path.GetExtension(item.ImageFile.FileName);

                    try
                    {
                        var result = AwsHelpers.UploadImage(fileName, item.ImageFile);

                        item.Program.Image = fileName;
                    }
                    catch (Exception ex)
                    {
                        AlertMessage = "Your program has been saved successfully, but there was a problem uploading your image.  Please contact our support if you continue to have problems.";
                    }
                }

                using (var provider = new ProgramProvider())
                {
                    provider.Update(item.Program);
                }

                return Redirect("/Site/ViewProgram/" + item.Program.Id);
            }

            return View(DefaultViews.CreateEdit, item);
        }

        [HttpGet]
        [RequireUser]
        public ActionResult Cancel(string id)
        {
            using (var programProvider = new ProgramProvider())
            {
                var program = programProvider.GetProgram(id);

                if (!program.ProgramUsers.Any(x => x.UserType.Equals(ProgramUserTypes.Administrator) && x.UserId.Equals(SessionVariables.CurrentUser.Id)) && !SessionVariables.CurrentUser.SuperAdmin)
                {
                    return RedirectToAction("AccessDenied", "Error");
                }

                program.Program.Status = ProgramStatus.Cancelled;

                programProvider.Update(program.Program);

                return DefaultRedirect;
            }
        }

        [HttpGet]
        [RequireSuperAdmin]
        public ActionResult Delete(string id)
        {
            using (var provider = new ProgramProvider())
            {
                provider.Delete(id);

                return DefaultRedirect;
            }
        }

        #endregion

        #region Flagging

        [HttpGet]
        public ActionResult Flag(string id)
        {
            using (var provider = new ProgramProvider())
            {
                provider.Flag(id, true);
            }

            AlertMessage = "You have flagged this program for review";

            return Redirect("/Site/ViewProgram/" + id);
        }

        [HttpGet]
        public ActionResult UnFlag(string id)
        {
            using (var provider = new ProgramProvider())
            {
                provider.Flag(id, false);
            }

            AlertMessage = "You have un-flagged this program.";
            AlertMessageType = AlertMessageTypes.Success;

            return Redirect("/Site/ViewProgram/" + id);
        }

        #endregion

        #region Create / Edit Approvals

        [HttpGet]
        public ActionResult _AddProgramApproval(string programId)
        {
            var approval = new ProgramApprovalSD();

            return PartialView("_AddProgramApproval", approval);
        }

        [HttpPost]
        public ActionResult _AddProgramApproval(ProgramApprovalSD model)
        {
            if (ModelState.IsValid)
            {
                using (var provider = new ProgramApprovalProvider())
                {
                    model.Id = DataAccess.Utilities.GenerateUniqueID();
                    model.UserId = SessionVariables.CurrentUser.Id;
                    model.DateCreated = DateTime.Now;

                    provider.Insert(model);
                }

                AlertMessage = "You have successfully approved this program.";
                AlertMessageType = AlertMessageTypes.Success;

                return RedirectTo("/Programs/Edit/" + model.ProgramId);
            }

            return PartialView("_AddProgramApproval", model);
        }

        #endregion

        #region Create / Edit Administrators & Participants

        [HttpGet]
        [RequireSuperAdmin]
        public ActionResult _AddUser(string programId, string userType)
        {
            var item = new ProgramUserSD() { ProgramId = programId, UserType = userType };

            return PartialView("_AddUser", item);
        }

        [HttpPost]
        [RequireSuperAdmin]
        public ActionResult _AddUser(ProgramUserSD item)
        {
            if (!string.IsNullOrEmpty(item.UserId))
            {
                using (var provider = new ProgramUserProvider())
                {
                    item.Id = DataAccess.Utilities.GenerateUniqueID();
                    item.Status = ProgramUserStatus.Pending;

                    provider.Insert(item);

                    AlertMessage = "Successfully added a new user to the " + item.UserType + "group";
                    AlertMessageType = AlertMessageTypes.Success;

                    return RedirectTo("/Programs/Edit/" + item.ProgramId);
                }
            }

            ModelState.AddModelError("", "Please select a user");

            return PartialView("_AddUser", item);
        }

        [HttpGet]
        public ActionResult _AddProgramUser(string programId, string userType)
        {
            var item = new ProgramUserSD() { ProgramId = programId, UserType = userType };

            return PartialView("_AddProgramUser", item);
        }

        [HttpPost]
        public ActionResult _AddProgramUser(ProgramUserSD item)
        {
            if (ModelState.IsValid)
            {
                using (var provider = new ProgramUserProvider())
                {
                    item.Id = DataAccess.Utilities.GenerateUniqueID();
                    item.Status = ProgramUserStatus.Pending;

                    provider.Insert(item);

                    var email = new EmailSD
                    {
                        Id = DataAccess.Utilities.GenerateUniqueID(),
                        DateCreated = DateTime.Now,
                        To = item.InvitedEmail,
                        Subject = "Village Creed - Invite",
                        Message =
                            string.Format(
                                "<p>Hi,</p><p>{0} has sent you an invite to join a program as a {1}.  You can view this invite by clicking the URL below.</p><p><a href='{2}'>{2}</a></p><p>- The Village Creed Team</p>",
                                SessionVariables.CurrentUser.DisplayName, item.UserType.ToLower(),
                                ApplicationCache.Instance.SiteUrl + "/Dashboard/ViewProgramInvite/" + item.Id)
                    };

                    Emailer.SendEmail(email);

                    return RedirectTo("/Programs/Edit/" + item.ProgramId);
                }
            }

            return PartialView("_AddProgramUser", item);
        }

        [HttpGet]
        [RequireUser]
        public ActionResult _RemoveProgramUser(string programUserId)
        {
            using (var programUserProvider = new ProgramUserProvider())
            {
                var programUser = programUserProvider.Get(programUserId);
                programUserProvider.Delete(programUserId);

                AlertMessage = string.Format("You have successfully removed a program user.");
                AlertMessageType = AlertMessageTypes.Success;

                return Redirect("/Programs/Edit/" + programUser.ProgramId);
            }
        }

        [HttpGet]
        [RequireUser]
        public ActionResult _MakeProgramUser(string programUserId, string programUserType)
        {
            using (var programUserProvider = new ProgramUserProvider())
            {
                var programUser = programUserProvider.Get(programUserId);

                if (ProgramUserTypes.Items.Contains(programUserType))
                {
                    programUser.UserType = programUserType;

                    programUserProvider.Update(programUser);

                    AlertMessage = string.Format("You have successfully made this program user a {0}.", programUser.UserType);
                    AlertMessageType = AlertMessageTypes.Success;
                }
                else
                {
                    AlertMessage = "<strong>" + programUserType + "</strong> is not a valid program user type.";
                    AlertMessageType = AlertMessageTypes.Failure;
                }

                return Redirect("/Programs/Edit/" + programUser.ProgramId);
            }
        }

        #endregion

        #region Create / Edit Attendance

        [HttpGet]
        [RequireUser]
        public ActionResult _AddAttendance(string userId, string programId)
        {
            using (var programProvider = new ProgramProvider())
            {
                var program = programProvider.Get(programId);

                var attendance = new ProgramAttendanceSD
                {
                    UserId = userId, 
                    ProgramId = programId, 
                    Method = ProgramAttendanceMethods.Manual, 
                    Hours = Convert.ToDecimal(program.EventLength)
                };

                return PartialView("_AddAttendance", attendance);
            }
        }

        [HttpPost]
        [RequireUser]
        public ActionResult _AddAttendance(ProgramAttendanceSD item)
        {
            if (ModelState.IsValid)
            {
                using (var provider = new ProgramAttendanceProvider())
                {
                    item.Id = DataAccess.Utilities.GenerateUniqueID();
                    item.DateCreated = DateTime.Now;
                    item.CreatedBy = SessionVariables.CurrentUser.Id;

                    provider.Insert(item);
                }

                return ReloadCurrentPage;
            }

            return PartialView("_AddAttendance", item);
        }

        public ActionResult _ViewAttendance(string userId, string programId)
        {
            using (var provider = new ProgramAttendanceProvider())
            {
                var items = provider.GetAttendance(userId, programId);

                return PartialView("_ViewAttendance", items);
            }
        }

        #endregion

        #region Create / Edit Comments

        [HttpPost]
        [RequireUser]
        public ActionResult _AddComment(ProgramCommentSD model)
        {
            using (var provider = new ProgramCommentProvider())
            {
                model.Id = DataAccess.Utilities.GenerateUniqueID();
                model.DateCreated = DateTime.Now;
                model.UserId = SessionVariables.CurrentUser.Id;

                provider.Insert(model);
            }

            using (var provider = new ProgramProvider())
            {
                var program = provider.GetProgram(model.ProgramId);

                return PartialView("_Comments", program);
            }
        }

        [HttpGet]
        [RequireUser]
        public ActionResult _DeleteComment(string id, string programId)
        {
            using (var provider = new ProgramCommentProvider())
            {
                provider.Delete(id);
            }

            using (var provider = new ProgramProvider())
            {
                var program = provider.GetProgram(programId);

                return PartialView("_Comments", program);
            }
        }

        #endregion

        #region Think Tank

        [RequireUser]
        public ActionResult ThinkTank(string type, string typeId, string locationType, string visibility)
        {
            ViewBag.LocationType = locationType;
            ViewBag.Visibility = visibility;

            using (var provider = new ProgramProvider())
            {
                var programs = provider.GetForLocationType(locationType, visibility, typeId);

                return View(programs);
            }
        }

        #endregion

        #region Badges

        [HttpGet]
        [RequireUser]
        public ActionResult _AddBadge(string programId)
        {
            var badge = new BadgeSD
            {
                Id = DataAccess.Utilities.GenerateUniqueID(), 
                ProgramId = programId
            };

            return PartialView("_AddBadge", badge);   
        }

        [HttpPost]
        [RequireUser]
        public ActionResult _AddBadge(BadgeSD item)
        {
            if (ModelState.IsValid)
            {
                using (var provider = new BadgeProvider())
                {
                    provider.Insert(item);
                }

                AlertMessage = "Successfully added a new badge";
                AlertMessageType = AlertMessageTypes.Success;

                return ReloadCurrentPage;
            }

            return PartialView("_AddBadge", item);
        }

        [HttpGet]
        [RequireUser]
        public ActionResult RemoveBadge(string badgeId, string programId)
        {
            using (var provider = new BadgeProvider())
            {
                provider.Delete(badgeId);
            }

            return Redirect("/Programs/Edit/" + programId);
        }

        [HttpGet]
        [RequireUser]
        public ActionResult _AddUserBadge(string userId, string programId)
        {
            ViewBag.ProgramId = programId;

            var userBadge = new UserBadgeSD
            {
                Id = DataAccess.Utilities.GenerateUniqueID(), 
                UserId = userId
            };

            return PartialView("_AddUserBadge", userBadge);
        }

        [HttpPost]
        [RequireUser]
        public ActionResult _AddUserBadge(UserBadgeSD item)
        {
            if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(item.BadgeId))
                {
                    using (var provider = new UserBadgeProvider())
                    {
                        provider.Insert(item);
                    }

                    AlertMessage = "Successfully added a new badge to user";
                    AlertMessageType = AlertMessageTypes.Success;

                    return ReloadCurrentPage;
                }
                else
                {
                    ModelState.AddModelError("", "Please select a valid badge");
                }
            }

            return PartialView("_AddUserBadge", item);
        }

        #endregion

        #region Ajax Save

        [HttpPost]
        [RequireUser]
        public ActionResult SaveStatus(ProgramViewSD model, string status)
        {
            if (!string.IsNullOrWhiteSpace(status))
            {
                model.Program.Status = status;
            }

            Save(model);

            AlertMessage = "Your program has been saved...";
            AlertMessageType = AlertMessageTypes.Success;

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [RequireUser]
        public void Save(ProgramViewSD model)
        {
            try
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    var fileName = DataAccess.Utilities.GenerateUniqueID() + Path.GetExtension(model.ImageFile.FileName);

                    try
                    {
                        var result = AwsHelpers.UploadImage(fileName, model.ImageFile);

                        model.Program.Image = fileName;
                    }
                    catch (Exception ex)
                    {
                        string s = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                string s = string.Empty;
            }

            using (var projectProvider = new ProgramProvider())
            {
                if (model.New)
                {
                    try
                    {
                        projectProvider.Insert(model.Program);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.ContainsValue("PRIMARY"))
                        {
                            projectProvider.Update(model.Program);
                        }
                    }
                }
                else
                {
                    projectProvider.Update(model.Program);
                }
            }
        }

        #endregion
    }
}
