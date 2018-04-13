using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Linq;

namespace TestSVN
{
    public static class SVNBusiness
    {
        #region 用户管理
        public static bool CreateUser(string userName, string password)
        {
            var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_User", null);
            ManagementBaseObject @params = svn.GetMethodParameters("Create");
            @params["Name"] = userName.Trim();
            @params["Password"] = password.Trim();
            svn.InvokeMethod("Create", @params, null);
            return true;
        }

        public static bool DeleteUser(string userName)
        {
            var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_User", null);
            ManagementBaseObject @params = svn.GetMethodParameters("Delete");
            @params["Name"] = userName.Trim();
            svn.InvokeMethod("Delete", @params, null);
            return true;
        }
        public static bool SetPassword(string userName, string newPassword)
        {
            var userObj = new ManagementObject(@"root\VisualSVN", string.Format("VisualSVN_User.Name='{0}'", userName), null);
            ManagementBaseObject @params = userObj.GetMethodParameters("SetPassword");
            @params["Password"] = newPassword;
            userObj.InvokeMethod("SetPassword", @params, null);
            return true;
        }

        public static List<string> GetUsers()
        {
            List<string> result = new List<string>();
            //try
            //{
            var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_User", null);
            var instances = svn.GetInstances();
            foreach (var instance in instances)
            {
                result.Add(instance.Properties["Name"].Value.ToString());
            }
            //}
            //catch (Exception)
            //{
            //    throw;
            //}
            return result;
        }
        #endregion

        #region 仓储管理
        /// <summary>
        /// 创建仓储，需要管理员权限
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool CreateRepository(string name)
        {
            var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_Repository", null);
            ManagementBaseObject @params = svn.GetMethodParameters("Create");
            @params["Name"] = name.Trim();
            svn.InvokeMethod("Create", @params, null);
            return true;
        }
        /// <summary>
        /// 查看仓储下的目录和文件
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<string> GetRepositoryFiles(string repository, string path = "/")
        {
            List<string> folders = new List<string>();
            ManagementObject repositoryObj = GetRepositoryObject(repository);
            ManagementBaseObject inParameters = repositoryObj.GetMethodParameters("GetChildren");
            inParameters["Path"] = path;
            ManagementBaseObject outParameters = repositoryObj.InvokeMethod("GetChildren", inParameters, null);
            var childrens = outParameters["Children"] as ManagementBaseObject[];

            foreach (var children in childrens)
            {
                folders.Add(children.GetPropertyValue("Path").ToString()?.Replace(path, "").Replace("/", ""));
            }
            return folders;
        }
        /// <summary>
        /// 在仓储下创建文件夹
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool CreateRepositoryPath(string repository, string path = "/")
        {
            ManagementObject repoObject = GetRepositoryObject(repository);
            ManagementBaseObject inParams = repoObject.GetMethodParameters("CreateFolders");
            inParams["Folders"] = new string[] { path };
            inParams["Message"] = "";
            repoObject.InvokeMethod("CreateFolders", inParams, null);
            return true;
        }
        /// <summary>
        /// 获取所有仓储列表
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllRepositories()
        {
            List<string> result = new List<string>();
            var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_Repository", null);
            var instances = svn.GetInstances();
            foreach (var instance in instances)
            {
                result.Add(instance.Properties["Name"].Value.ToString());
            }
            return result;
        }
        #endregion

        #region 权限管理
        /// <summary>
        /// 设置权限
        /// </summary>
        /// <param name="userName">用户</param>
        /// <param name="repository">仓储</param>
        /// <param name="path">路径</param>
        /// <param name="accessLevel">权限级别</param>
        /// <returns></returns>
        public static bool SetPermission(string userName, string repository, string path = "/",
            AccessLevel accessLevel = AccessLevel.NoAccess)
        {
            return SetPermission(new List<string> { userName }, repository, path, accessLevel);
        }
        /// <summary>
        /// 设置权限
        /// </summary>
        /// <param name="userNames">用户列表</param>
        /// <param name="repository">仓储</param>
        /// <param name="path">路径</param>
        /// <param name="accessLevel">权限级别</param>
        /// <returns></returns>
        public static bool SetPermission(List<string> userNames, string repository, string path = "/",
            AccessLevel accessLevel = AccessLevel.NoAccess)
        {
            try
            {
                List<Permission> permissions = GetPermissions(repository, path);
                List<string> groups = GetAllGroups();//判断是否用户组，给ClassType赋值
                foreach (string name in userNames)
                {
                    if (!permissions.Any(p => p.Name == name))
                    {
                        Permission newPermision = new Permission { Name = name, AccessLevel = accessLevel };
                        if (groups.Any(g => g == name)) newPermision.ClassType = "VisualSVN_Group";
                        else if (name.ToLower() == "everyone") newPermision.ClassType = "VisualSVN_Everyone";
                        else newPermision.ClassType = "VisualSVN_User";
                        permissions.Add(newPermision);
                    }
                    else permissions.First(p => p.Name == name).AccessLevel = accessLevel;
                }
                SetPermissions(repository, path, permissions);
                return true;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        /// <summary>
        /// 获取某个用户权限
        /// </summary>
        /// <param name="userName">用户</param>
        /// <param name="repository">仓储</param>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static AccessLevel GetPermission(string userName, string repository, string path = "/")
        {
            var permissions = GetPermissions(repository, path);
            if (!permissions.Any(p => p.Name == userName)) return AccessLevel.NoAccess;
            else return permissions.First(p => p.Name == userName).AccessLevel;
        }
        #endregion

        #region 用户组
        public static bool CreateGroup(string name)
        {
            try
            {
                var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_Group", null);
                ManagementBaseObject @params = svn.GetMethodParameters("Create");
                @params["Name"] = name.Trim();
                @params["Members"] = new ManagementObject[0];
                svn.InvokeMethod("Create", @params, null);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        public static bool DeleteGroup(string name)
        {
            try
            {
                var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_Group", null);
                ManagementBaseObject @params = svn.GetMethodParameters("Create");
                @params["Name"] = name.Trim();
                svn.InvokeMethod("Delete", @params, null);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        public static List<string> GetAllGroups()
        {
            List<string> result = new List<string>();
            try
            {
                var svn = new ManagementClass(@"root\VisualSVN", "VisualSVN_Group", null);
                var instances = svn.GetInstances();
                foreach (var instance in instances)
                {
                    result.Add(instance.Properties["Name"].Value.ToString());
                }
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public static List<string> GetGroupsMembers(string name)
        {
            List<string> result = new List<string>();
            try
            {
                var members = GetGroupsMembersObject(name);
                foreach (var member in members)
                {
                    var memberName = member.GetPropertyValue("Name").ToString();
                    if (!result.Contains(memberName)) result.Add(memberName);
                }
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public static bool SetGroups(string name, string[] members)
        {
            try
            {
                List<ManagementObject> memberObjects = new List<ManagementObject>();
                var subversionAccount = new ManagementClass(@"root\VisualSVN", "VisualSVN_User", null);
                foreach (var m in members)
                {
                    ManagementObject instance = subversionAccount.CreateInstance();
                    instance["Name"] = m;
                    memberObjects.Add(instance);
                }

                var group = new ManagementObject(@"root\VisualSVN", "VisualSVN_Group.Name='" + name + "'", null);
                var inparams = group.GetMethodParameters("SetMembers");
                inparams["Members"] = memberObjects.ToArray();
                group.InvokeMethod("SetMembers", inparams, null);
                return true;
            }
            catch (Exception)
            {
                throw;
            }

        }
        #endregion

        #region 私有方法
        private static List<ManagementBaseObject> GetGroupsMembersObject(string name)
        {
            List<ManagementBaseObject> result = new List<ManagementBaseObject>();
            try
            {
                var svn = new ManagementObject(@"root\VisualSVN", string.Format("VisualSVN_Group.Name='{0}'", name), null);
                ManagementBaseObject outParameters = svn.InvokeMethod("GetMembers", null, null);
                if (outParameters != null)
                    foreach (ManagementBaseObject p in (ManagementBaseObject[])outParameters["Members"])
                    {
                        result.Add(p);
                    }
            }
            catch (Exception e)
            {
                throw e;
            }
            return result;
        }
        /// <summary>
        ///     根据仓库名取得仓库实体
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static ManagementObject GetRepositoryObject(string name)
        {
            return new ManagementObject(@"root\VisualSVN", string.Format("VisualSVN_Repository.Name='{0}'", name), null);
        }

        public static List<Permission> GetPermissions(string repositoryName, string path, List<Permission> permissions = null)
        {
            if (permissions == null) permissions = new List<Permission>();

            ManagementObject repository = GetRepositoryObject(repositoryName);
            ManagementBaseObject inParameters = repository.GetMethodParameters("GetSecurity");
            inParameters["Path"] = path;
            ManagementBaseObject outParameters = repository.InvokeMethod("GetSecurity", inParameters, null);

            if (outParameters != null)
                foreach (ManagementBaseObject p in (ManagementBaseObject[])outParameters["Permissions"])
                {
                    var account = (ManagementBaseObject)p["Account"];
                    var name = account.GetPropertyValue("Name").ToString();
                    var classType = account.ClassPath.ToString();
                    var accessLevel = (AccessLevel)p["AccessLevel"];

                    if (permissions.Any(permision => permision.Name == name && permision.ClassType == classType)) continue;
                    permissions.Add(new Permission { Name = name, ClassType = classType, AccessLevel = accessLevel });
                }
            if (path.Length > 1)//不是根目录 循环父节点的权限
            {
                string parentPath = path.Substring(0, path.LastIndexOf("/"));
                parentPath = string.IsNullOrEmpty(parentPath) ? "/" : parentPath;
                return GetPermissions(repositoryName, parentPath, permissions);
            }
            return permissions;
        }
        private static void SetPermissions(string repositoryName, string path,
                                          IEnumerable<Permission> permissions)
        {
            ManagementObject repository = GetRepositoryObject(repositoryName);
            ManagementBaseObject inParameters = repository.GetMethodParameters("SetSecurity");
            inParameters["Path"] = path;
            IEnumerable<ManagementObject> permissionObjects =
                permissions.Select(p => GetPermissionObject(p));
            inParameters["Permissions"] = permissionObjects.ToArray();
            repository.InvokeMethod("SetSecurity", inParameters, null);
        }
        private static ManagementObject GetPermissionObject(Permission permission)
        {
            var accountClass = new ManagementClass(@"root\VisualSVN", permission.ClassType, null);
            var entryClass = new ManagementClass(@"root\VisualSVN",
                                                 "VisualSVN_PermissionEntry", null);
            ManagementObject account = accountClass.CreateInstance();
            if (account != null) account["Name"] = permission.Name;
            ManagementObject entry = entryClass.CreateInstance();
            if (entry != null)
            {
                entry["AccessLevel"] = permission.AccessLevel;
                entry["Account"] = account;
                return entry;
            }
            return null;
        }
        #endregion
    }
    public enum AccessLevel : uint
    {
        NoAccess = 0,
        ReadOnly = 1,
        ReadWrite = 2
    }

    public class Permission
    {
        public string Name { get; set; }

        public AccessLevel AccessLevel { get; set; }

        public string ClassType { get; set; }
    }
}
