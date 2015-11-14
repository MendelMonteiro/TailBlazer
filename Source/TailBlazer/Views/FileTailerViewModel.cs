using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using TailBlazer.Domain.FileHandling;
using TailBlazer.Domain.Infrastructure;
using TailBlazer.Infrastucture;

namespace TailBlazer.Views
{
    public class FileTailerViewModel: AbstractNotifyPropertyChanged, IDisposable, IScrollController
    {
        private readonly IDisposable _cleanUp;
        private readonly ReadOnlyObservableCollection<LineProxy> _data;
       // private readonly ISubject<ScrollInfo> _userScrollRequested = new Subject<ScrollInfo>();

        public string File { get; }
        public ReadOnlyObservableCollection<LineProxy> Lines => _data;
        public AutoScroller AutoScroller { get; } = new AutoScroller();

        private string _searchText;
        private bool _autoTail;
        private string _lineCountText;
        private int _firstLine;
        private int _matchedLineCount;
        private int _pageSize;
        private ScrollInfoArgs _scrollInfoArgs;

        public FileTailerViewModel(ILogger logger,ISchedulerProvider schedulerProvider, FileInfo fileInfo)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (schedulerProvider == null) throw new ArgumentNullException(nameof(schedulerProvider));
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));

            File = fileInfo.FullName;
            AutoTail = true;
            
            var filterRequest = this.WhenValueChanged(vm => vm.SearchText).Throttle(TimeSpan.FromMilliseconds(125));

            var autoChanged = this.WhenValueChanged(vm => vm.AutoTail);
            var firstLineChanged = this.WhenValueChanged(vm => vm.FirstLine);
            var pageSizeChanged = this.WhenValueChanged(vm => vm.PageSize);

            var scroller = autoChanged.CombineLatest(firstLineChanged, pageSizeChanged, 
                        (auto, first, pageSize) => new  { auto, first, pageSize})
                    .Select(args =>
                    {
                        return args.auto 
                                    ? new ScrollRequest(args.pageSize) 
                                    : new ScrollRequest(args.pageSize, args.first);
                    })
                    .DistinctUntilChanged();

            //var autotail = this.WhenValueChanged(vm => vm.AutoTail)
            //                .CombineLatest(_userScrollRequested, (auto, user) =>
            //                {
            //                    return auto ? new ScrollRequest(user.Rows) 
            //                    : new ScrollRequest(user.Rows, user.FirstIndex+1);
            //                }).DistinctUntilChanged();

            var tailer = new FileTailer(fileInfo, filterRequest, scroller);


            //create user display for count line count
            var lineCounter = tailer.TotalLines.CombineLatest(tailer.MatchedLines,(total,matched)=>
            {
                return total == matched 
                    ? $"File has {total.ToString("#,###")} lines" 
                    : $"Showing {matched.ToString("#,###")} of {total.ToString("#,###")} lines";
            })
            .Subscribe(text => LineCountText=text);
            

            //load lines into observable collection
            var loader = tailer.Lines.Connect()
                .Buffer(TimeSpan.FromMilliseconds(125)).FlattenBufferResult()
                .Transform(line => new LineProxy(line))
                .Sort(SortExpressionComparer<LineProxy>.Ascending(proxy => proxy.Number))
                .ObserveOn(schedulerProvider.MainThread)
                .Bind(out _data)
                .Do(_=> AutoScroller.ScrollToEnd())
                .Subscribe(a => logger.Info(a.Adds.ToString()), ex => logger.Error(ex, "Oops"));


            //monitor matching lines and start index 
            //update local info so the virtual scroll panel can bind to them
            var matchedLinesMonitor = tailer.MatchedLines.Subscribe(matched => MatchedLineCount = matched);

            var firstIndexMonitor = tailer.Lines.Connect()
                .QueryWhenChanged(lines =>
                {
                    //use zero based index rather than line number
                    return lines.Count == 0 ? 0 : lines.Select(l => l.Number).Min();
                }).Subscribe(first => FirstLine = first - 1);


            _cleanUp = new CompositeDisposable(tailer, 
                lineCounter, 
                loader,
                matchedLinesMonitor);

        }

        void IScrollController.ScrollInfoChanged(ScrollInfoArgs info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            _scrollInfoArgs = info;
            //index zero based for scroller
            FirstLine = info.FirstRowIndex +1 ;
            PageSize = info.PageSize;

            if ((info.ScrollInfo.VerticalOffset < _scrollInfoArgs.ScrollInfo.VerticalOffset) &&
                info.Producer == ScollProducer.User)
                AutoTail = false;



            //  _userScrollRequested.OnNext(info);
        }

        public bool AutoTail
        {
            get { return _autoTail; }
            set { SetAndRaise(ref _autoTail, value); }
        }

        public int FirstLine
        {
            get { return _firstLine; }
            set { SetAndRaise(ref _firstLine, value); }
        }

        private int PageSize
        {
            get { return _pageSize; }
            set { SetAndRaise(ref _pageSize, value); }
        }



        public int MatchedLineCount
        {
            get { return _matchedLineCount; }
            set { SetAndRaise(ref _matchedLineCount, value); }
        }

        public string SearchText
        {
            get { return _searchText; }
            set { SetAndRaise(ref _searchText, value); }
        }

        public string LineCountText
        {
            get { return _lineCountText; }
            set { SetAndRaise(ref _lineCountText, value); }
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }


    }
}
