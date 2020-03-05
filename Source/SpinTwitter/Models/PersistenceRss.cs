namespace SpinTwitter.Models
{
    public class PersistenceRss
    {
        public uint? LastEntered { get; set; }
        public uint? LastVerified { get; set; }
        public override string ToString()
        {
            return $"Entered:{LastEntered:#,##0} Verified:{LastVerified:#,##0}";
        }
    }
}
