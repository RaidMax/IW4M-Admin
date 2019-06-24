using IW4MAdmin.Application;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace Tests
{
    [Collection("ManagerCollection")]
    public class ManagerTests
    {
        readonly ApplicationManager Manager;

        public ManagerTests(ManagerFixture fixture)
        {
            Manager = fixture.Manager;
        }

        [Fact]
        public void AreCommandNamesUnique()
        {
            bool test = Manager.GetCommands().Count == Manager.GetCommands().Select(c => c.Name).Distinct().Count();
            Assert.True(test, "command names are not unique");
        }

        [Fact]
        public void AreCommandAliasesUnique()
        {
            var mgr = Manager;
            bool test = mgr.GetCommands().Count == mgr.GetCommands().Select(c => c.Alias).Distinct().Count();

            foreach (var duplicate in mgr.GetCommands().GroupBy(_cmd => _cmd.Alias).Where(_grp => _grp.Count() > 1).Select(_grp => new { Command = _grp.First().Name, Alias = _grp.Key }))
            {
                Debug.WriteLine($"{duplicate.Command}: {duplicate.Alias}");
            }

            Assert.True(test, "command aliases are not unique");
        }

        [Fact]
        public void PrintCommands()
        {
            var sb = new StringBuilder();

            sb.AppendLine("|Name              |Alias|Description                                                                               |Requires Target|Syntax           |Required Level|");
            sb.AppendLine("|--------------| -----| --------------------------------------------------------| -----------------| -------------| ----------------|");

            foreach (var command in Manager.GetCommands().OrderByDescending(c => c.Permission).ThenBy(c => c.Name))
            {
                sb.AppendLine($"|{command.Name}|{command.Alias}|{command.Description}|{command.RequiresTarget}|{command.Syntax.Substring(8).EscapeMarkdown()}|{command.Permission}|");
            }

            Assert.True(false, sb.ToString());
        }
    }
}

