using System;

namespace Squadra.Campaign
{
    /// <summary>
    /// Groups the ordered missions into map pages. Every page has the same number of slots; the last page
    /// fills its leftmost slots and leaves the rest empty.
    /// </summary>
    public readonly struct MissionPages
    {
        public const int MinPerPage = 1;
        public const int MaxPerPage = 10;
        public const int DefaultPerPage = 5;

        public readonly int MissionCount;
        public readonly int PerPage;

        public MissionPages(int missionCount, int perPage)
        {
            MissionCount = Math.Max(0, missionCount);
            PerPage = Math.Min(MaxPerPage, Math.Max(MinPerPage, perPage));
        }

        /// <summary>At least one, so an empty catalog still has a page to show.</summary>
        public int PageCount => Math.Max(1, (MissionCount + PerPage - 1) / PerPage);

        /// <summary>True when the missions do not fit on one page, which is when the map shows its arrows.</summary>
        public bool HasSeveralPages => MissionCount > PerPage;

        public int ClampPage(int page)
        {
            return Math.Min(PageCount - 1, Math.Max(0, page));
        }

        public int PageOf(int missionIndex)
        {
            return ClampPage(missionIndex / PerPage);
        }

        /// <summary>Index of the mission in the page's first slot.</summary>
        public int FirstOnPage(int page)
        {
            return ClampPage(page) * PerPage;
        }

        /// <summary>How many of the page's slots hold a mission.</summary>
        public int CountOnPage(int page)
        {
            return Math.Max(0, Math.Min(PerPage, MissionCount - FirstOnPage(page)));
        }

        public bool HasPrevious(int page)
        {
            return ClampPage(page) > 0;
        }

        public bool HasNext(int page)
        {
            return ClampPage(page) < PageCount - 1;
        }
    }
}
