namespace ReadMeApp.Models;

public enum ReadingStatus
{
    Wishlist = 0,  // 읽고 싶은 책
    Reading = 1,   // 읽는 중
    Completed = 2, // 완독
    Paused = 3,    // 잠시 중단
    Draft = 4      // 독서록 임시 저장
}
