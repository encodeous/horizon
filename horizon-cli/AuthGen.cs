using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using horizon;

namespace horizon_cli
{
    class AuthGen
    {
        List<UserPermission> users = new List<UserPermission>();
        public AuthGen()
        {

        }

        public void Generate()
        {
            Console.Write("Please specify a file to save or load: ");
            string path = Console.ReadLine();
            if (File.Exists(path))
            {
                Console.WriteLine("Attempting to load an existing configuration");
                users = PermissionHandler.GetPermissionInfo(path);
            }
            while (true)
            {
                Console.Write("Please choose an option:\n" +
                              "a. Add New User\n" +
                              "b. Remove User\n" +
                              "c. List all Users\n" +
                              "d. View a User's settings'\n" +
                              "e. Save and Exit\n");
                Console.Write("Selection: ");
                string s = Console.ReadLine()?.ToLower();
                if (s.StartsWith("a"))
                {
                    AddUser();
                }
                else if (s.StartsWith("b"))
                {
                    Console.Write("Please enter a username: ");
                    string name = Console.ReadLine()?.ToLower();
                    name = name.ToLower().Trim();
                    foreach(UserPermission p in users)
                    {
                        if (p.UserId == name)
                        {
                            users.Remove(p);
                            Console.WriteLine("The specified user has been removed!");
                            break;
                        }
                    }
                }
                else if (s.StartsWith("c"))
                {
                    Console.WriteLine("Users: ");
                    foreach(UserPermission p in users)
                    {
                        if (p.Administrator)
                        {
                            Console.WriteLine($"{p.UserId} - Admin");
                        }
                        else
                        {
                            Console.WriteLine($"{p.UserId}");
                        }
                    }
                }
                else if (s.StartsWith("d"))
                {
                    Console.Write("Please enter a username: ");
                    string name = Console.ReadLine()?.ToLower();
                    name = name.ToLower().Trim();

                    bool found = false;
                    foreach(UserPermission p in users)
                    {
                        if (p.UserId == name)
                        {
                            string str = $"Info about {p.UserId}:\n";
                            if (p.Administrator) str += "Administrator\n";
                            if (p.AllowAnyPort) str += "Allow Any Port\n";
                            if (p.AllowAnyServer) str += "Allow Any Server\n";
                            if (p.AllowedRemotePorts.Count != 0) str += "Allowed Ports:" + string.Join(' ', p.AllowedRemotePorts) + "\n";
                            if (p.DisallowedRemotePorts.Count != 0) str += "Disallowed Ports:" + string.Join(' ', p.DisallowedRemotePorts) + "\n";
                            if (p.AllowedRemoteServers.Count != 0) str += "Allowed Servers:" + string.Join(' ', p.AllowedRemoteServers) + "\n";
                            if (p.DisallowedRemoteServers.Count != 0) str += "Disallowed Servers:" + string.Join(' ', p.DisallowedRemoteServers) + "\n";
                            str += "Token: " + p.UserToken;
                            Console.WriteLine(str);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        Console.WriteLine("The specified user was not found");
                    }
                }
                else if (s.StartsWith("e"))
                {
                    PermissionHandler.SetPermissionInfo(path, users);
                    break;
                }
                else
                {
                    Console.WriteLine("Please Try Again.");
                }
            }
        }

        public void AddUser()
        {
            Console.Write("Please enter a username: ");
            string name = Console.ReadLine()?.ToLower();
            name = name.ToLower().Trim();
            if (name.Contains(" "))
            {
                Console.WriteLine("Names cannot contain spaces!");
                return;
            }
            foreach(UserPermission p in users)
            {
                if (p.UserId == name)
                {
                    Console.WriteLine("The specified user already exists!");
                    return;
                }
            }
            Console.Write("Please enter a user token: ");
            string token = Console.ReadLine();

            UserPermission user = new UserPermission();
            user.UserId = name;
            user.UserToken = token;

            Console.Write("Should this user be an administrator? (An administrator has all permissions and can access any servers): ");
            var admin = ReadYesNo();
            if (admin)
            {
                user.Administrator = true;
                users.Add(user);
                return;
            }

            Console.Write("Should this user be able to connect to any ports (except for forbidden ports)?: ");
            var port = ReadYesNo();
            if (port)
            {
                user.AllowAnyPort = true;
            }
            else
            {
                Console.Write("Please specify the ports the user can access (separated by spaces): ");
                user.AllowedRemotePorts = ReadIntList();
            }
            Console.Write("Please specify the ports the user is forbidden from accessing access (separated by spaces): ");
            user.DisallowedRemotePorts = ReadIntList();

            Console.Write("Should this user be able to connect to any remote hosts (except for forbidden remote hosts)?: ");
            var host = ReadYesNo();
            if (host)
            {
                user.AllowAnyServer = true;
            }
            else
            {
                Console.Write("Please specify the remote hosts the user can access (separated by spaces): ");
                user.AllowedRemoteServers = ReadList();
            }

            Console.Write("Please specify the remote hosts the user is forbidden from accessing access (separated by spaces): ");
            user.DisallowedRemoteServers = ReadList();

            Console.WriteLine("User has been successfully created!");
            users.Add(user);
        }

        public bool ReadYesNo()
        {
            Console.Write("[y/(n)]: ");
            string k = Console.ReadLine();
            return string.IsNullOrEmpty(k) || k.StartsWith("y");
        }

        public List<string> ReadList()
        {
            string k = Console.ReadLine();
            if(string.IsNullOrEmpty(k)) return new List<string>();
            return k.Split(" ").ToList();
        }

        public List<int> ReadIntList()
        {
            string k = Console.ReadLine();
            if(string.IsNullOrEmpty(k)) return new List<int>();
            return Array.ConvertAll(k.Split(" "), input => int.Parse(input)).ToList();
        }
    }
}
