using System;

namespace SharedLibraryCore.Dtos
{
    /// <summary>
    ///     data transfer class for audit information
    /// </summary>
    public class AuditInfo
    {
        private string newValue;

        private string oldValue;

        /// <summary>
        ///     name of the origin entity
        /// </summary>
        public string OriginName { get; set; }

        /// <summary>
        ///     id of the origin entity
        /// </summary>
        public int OriginId { get; set; }

        /// <summary>
        ///     name of the target entity
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        ///     id of the target entity
        /// </summary>
        public int? TargetId { get; set; }

        /// <summary>
        ///     when the audit event occured
        /// </summary>
        public DateTime When { get; set; }

        /// <summary>
        ///     what audit action occured
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        ///     additional comment data about the audit event
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        ///     previous value
        /// </summary>
        public string OldValue
        {
            get => oldValue ?? "--";
            set => oldValue = value;
        }

        /// <summary>
        ///     new value
        /// </summary>
        public string NewValue
        {
            get => newValue ?? "--";
            set => newValue = value;
        }
    }
}