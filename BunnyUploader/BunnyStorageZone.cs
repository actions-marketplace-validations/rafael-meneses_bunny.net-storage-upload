namespace BunnyUploader
{
    public class BunnyStorageZone
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Region { get; set; }

        public List<BunnyPullZone> PullZones { get; set; }
    }
}
