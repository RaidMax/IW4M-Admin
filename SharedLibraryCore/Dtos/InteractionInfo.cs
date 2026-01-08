using System.Collections.Generic;
using Data.Models.Client;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Dtos
{
    public class InteractionInfo
    {
        public int? EntityId { get; set; }
        public string InteractionId { get; set; }
        public InteractionType InteractionType { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DisplayMeta { get; set; }
        public string ActionPath { get; set; }
        public Dictionary<string, string> ActionMeta { get; set; } = new();
        public string ActionUri { get; set; }
        public EFClient.Permission? MinimumPermission { get; set; }
        public string PermissionEntity { get; set; }
        public string PermissionAccess { get; set; }
        public string Source { get; set; }
    }
}
