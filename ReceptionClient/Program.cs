namespace ReceptionClient
{
    internal static class Program
    {
        // Khai báo HttpClient dưới dạng Singleton dùng chung cho toàn ứng dụng
        // Để tránh lỗi Socket Exhaustion và DNS DNS cache staleness.
        public static readonly HttpClient ApiClient = new HttpClient 
        { 
            BaseAddress = new Uri("http://localhost:5004/") 
        };

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}