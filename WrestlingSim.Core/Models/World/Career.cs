using WrestlingSim.Engine;
using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A save. Everything that persists between sessions hangs off this: the promotion,
    /// the world clock, the roster whose state actually changes, the feud book, and the
    /// calendar of shows scheduled and run.
    ///
    /// This is the object the rest of the game was missing. Heat, freshness and momentum
    /// are only meaningful relative to a clock that advances and results that are kept —
    /// see docs/wrestling-reference/17-heat-and-getting-over.md.
    /// </summary>
    /// <summary>Whether an offer was taken, and why not.</summary>
    public sealed record SigningResult(bool Accepted, string? Reason, Contract? Contract, bool Pending = false)
    {
        public static SigningResult Signed(Contract c) => new(true, null, c);
        public static SigningResult Refused(string why) => new(false, why, null);

        /// <summary>An offer to somebody still under contract elsewhere, decided when that deal ends.</summary>
        public static SigningResult Lodged(string note) => new(true, note, null, Pending: true);
    }

    public class Career
    {
        /// <summary>Stable id so a browser can hold several saves side by side.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public required Promotion Promotion { get; set; }

        /// <summary>Today, in world time. Advancing this is the only way anything ages.</summary>
        public DateOnly CurrentDate { get; set; }

        /// <summary>The date the save began, so elapsed time can be reported.</summary>
        public DateOnly StartDate { get; set; }

        /// <summary>The roster this promotion can book. Mutated by results and persisted.</summary>
        public List<Wrestler> Roster { get; set; } = new();

        /// <summary>
        /// Cash and reputation. Opened by <see cref="OpenBooks"/> when a career starts, or
        /// when a career saved before money existed is loaded.
        /// </summary>
        public CareerBooks Books { get; set; } = new();

        /// <summary>
        /// What this promotion's audience has been shown lately — whose act has gone stale with
        /// them, and how alike the shows have been. See <see cref="AudienceModel"/>.
        /// </summary>
        public Audience Audience { get; set; } = new();

        /// <summary>
        /// How much of a wrestler's pull this audience still feels on a show of this type,
        /// 1.0 being all of it.
        /// </summary>
        public double Response(Wrestler w, ShowType type, int daysAhead = 0) =>
            AudienceModel.Response(Audience, w, Promotion.Tier, type, daysAhead);

        /// <summary>
        /// The television contract, at a tier that has television; null below it. Opened with
        /// the books, and renewed by the clock when its term runs out.
        /// </summary>
        public TvDeal? Television { get; set; }

        /// <summary>
        /// The promotion's roster: who is signed, or booked as a regular, and on what terms.
        /// Everyone else in <see cref="Roster"/> is on the open market — still bookable, by the
        /// night, at their asking price (<see cref="TalentMarket.PerDateAsk"/>).
        /// </summary>
        public List<Contract> Contracts { get; set; } = new();

        /// <summary>What happened to the roster lately, newest last. Capped.</summary>
        public List<ContractEvent> ContractLog { get; set; } = new();

        public Contract? ContractOf(Wrestler w) => Contracts.FirstOrDefault(c => c.WrestlerId == w.Id);

        /// <summary>Paid by the guarantee: their appearances cost nothing extra.</summary>
        public bool IsGuaranteed(Wrestler w) => ContractOf(w)?.IsGuaranteed == true;

        /// <summary>Only a promotion with television signs anybody. Below it, everybody works by the night.</summary>
        public bool CanSign => Promotion.HasTelevision;

        /// <summary>A day's guarantees.</summary>
        public decimal DailyPayroll => Contracts.Sum(c => c.DailyCost);

        public IEnumerable<Contract> ExpiringWithin(int days) =>
            Contracts.Where(c => c.DaysLeft(CurrentDate) is { } left && left <= days)
                     .OrderBy(c => c.End);

        // ── The world ────────────────────────────────────────────────────────

        /// <summary>The player, as a bidder.</summary>
        public const string PlayerId = "you";

        /// <summary>The other promotions in the world. See <see cref="World"/>.</summary>
        public List<RivalPromotion> Rivals { get; set; } = new();

        /// <summary>Released wrestlers who cannot work anywhere yet (doc 13 §3.2).</summary>
        public List<NonCompete> NonCompetes { get; set; } = new();

        /// <summary>Open offers for wrestlers whose deals are running out.</summary>
        public List<Bid> Bids { get; set; } = new();

        /// <summary>The industry's news, newest last. Capped.</summary>
        public List<NewsItem> News { get; set; } = new();

        /// <summary>What competition is doing to every price. 1.0 with two credible employers.</summary>
        public double WageFactor => Engine.World.WageFactor(this);

        /// <summary>What competition is doing to every audience. 1.0 with two credible employers.</summary>
        public double Interest => Engine.World.Interest(this);

        public decimal PerDateAsk(Wrestler w) => TalentMarket.PerDateAsk(w, WageFactor);
        public decimal GuaranteeAsk(Wrestler w, int years) => TalentMarket.GuaranteeAsk(w, years, WageFactor);

        /// <summary>What booking them tonight costs the player: nothing on a guarantee, their price otherwise.</summary>
        public decimal AppearanceFee(Wrestler w) => IsGuaranteed(w) ? 0m : PerDateAsk(w);

        public RivalPromotion? Rival(string id) => Rivals.FirstOrDefault(r => r.Id == id);

        /// <summary>The rival holding them on an exclusive deal, if any. A regular is nobody's exclusive.</summary>
        public RivalPromotion? SignedTo(Wrestler w) =>
            Rivals.FirstOrDefault(r => r.IsActive && r.ContractOf(w)?.IsGuaranteed == true);

        public NonCompete? NonCompeteOf(Wrestler w) =>
            NonCompetes.FirstOrDefault(n => n.WrestlerId == w.Id && n.Until > CurrentDate);

        /// <summary>Why the player cannot book them, or null if they can.</summary>
        public string? Unavailable(Wrestler w) =>
            SignedTo(w) is { } r ? $"signed to {r.Name}"
            : NonCompeteOf(w) is { } n ? $"under a non-compete until {n.Until:d MMM}"
            : null;

        /// <summary>Everybody on a card the player cannot put in the ring, and why.</summary>
        public IReadOnlyList<(Wrestler Wrestler, string Why)> UnavailableOnCard(ScheduledShow show) =>
            show.Card.SelectMany(i => i.Wrestlers).Distinct()
                .Select(w => (w, Unavailable(w)))
                .Where(x => x.Item2 is not null)
                .Select(x => (x.w, x.Item2!))
                .ToList();

        public Bid? BestRivalBid(Wrestler w) =>
            Bids.Where(b => b.WrestlerId == w.Id && b.Bidder != PlayerId).MaxBy(b => b.Annual);

        public Bid? PlayerBid(Wrestler w) =>
            Bids.FirstOrDefault(b => b.WrestlerId == w.Id && b.Bidder == PlayerId);

        public void AddNews(string text, bool aboutYou = false)
        {
            News.Add(new NewsItem { Date = CurrentDate, Text = text, AboutYou = aboutYou });
            if (News.Count > 80) News.RemoveAt(0);
        }

        /// <summary>Vacates anything they hold, because they are leaving.</summary>
        public void VacateTitles(Wrestler w, string reason)
        {
            foreach (var title in Titles.HeldBy(w))
                if (title.CurrentReign is { } reign)
                {
                    reign.Lost    = CurrentDate;
                    reign.Vacated = true;
                    reign.LostAt  = reason;
                }
        }

        /// <summary>
        /// Signs a wrestler to a guarantee, re-signs one whose deal is running out, or — for a
        /// rival's wrestler in the last sixty days of their deal — lodges an offer that they
        /// decide on when it ends.
        ///
        /// Doc 10 §6.2: a competing offer is the only real leverage a wrestler has. Re-signing
        /// somebody a rival has bid for means matching the bid.
        /// </summary>
        public SigningResult Sign(Wrestler w, decimal annual, int years)
        {
            if (!CanSign) return SigningResult.Refused("Only a promotion with television signs anybody. Below it, everybody works by the night.");
            if (NonCompeteOf(w) is { } nc) return SigningResult.Refused($"{w.RingName} is under a non-compete until {nc.Until:d MMM yyyy}.");

            years = Math.Clamp(years, TalentMarket.MinTermYears, TalentMarket.MaxTermYears);
            var ask = GuaranteeAsk(w, years);
            var plural = years == 1 ? "" : "s";

            if (SignedTo(w) is { } holder)
            {
                var theirs = holder.ContractOf(w)!;
                int left = theirs.DaysLeft(CurrentDate) ?? int.MaxValue;
                if (left > TalentMarket.ExpiryWarningDays)
                    return SigningResult.Refused($"{w.RingName} is signed to {holder.Name} until {theirs.End:d MMM yyyy}. Offers open {TalentMarket.ExpiryWarningDays} days before it ends.");
                if (annual < ask)
                    return SigningResult.Refused($"{w.RingName} wants ${ask:N0} a year for {years} year{plural}.");

                Bids.RemoveAll(b => b.WrestlerId == w.Id && b.Bidder == PlayerId);
                Bids.Add(new Bid { WrestlerId = w.Id, Bidder = PlayerId, Annual = annual, Years = years, Made = CurrentDate });
                Log(w, $"offered ${annual:N0} a year for {years} year{plural} — decides when the {holder.Name} deal ends");
                return SigningResult.Lodged($"Offer lodged. {w.RingName} decides when the {holder.Name} deal ends on {theirs.End:d MMM}.");
            }

            if (BestRivalBid(w) is { } rivalBid && rivalBid.Annual > ask)
            {
                ask = rivalBid.Annual;
                if (annual < ask)
                    return SigningResult.Refused($"{Rival(rivalBid.Bidder)?.Name ?? "A rival"} has offered ${ask:N0} a year. Match it or lose {w.RingName} when the deal ends.");
            }
            if (annual < ask) return SigningResult.Refused($"{w.RingName} wants ${ask:N0} a year for {years} year{plural}.");

            Contracts.RemoveAll(c => c.WrestlerId == w.Id);
            Bids.RemoveAll(b => b.WrestlerId == w.Id);
            var contract = new Contract
            {
                WrestlerId      = w.Id,
                Kind            = ContractKind.Guaranteed,
                AnnualGuarantee = annual,
                Start           = CurrentDate,
                End             = CurrentDate.AddYears(years)
            };
            Contracts.Add(contract);
            Log(w, $"signed for {years} year{plural} at ${annual:N0} a year");
            return SigningResult.Signed(contract);
        }

        /// <summary>Adds somebody to the regulars, below television. No contract, no cost until booked.</summary>
        public void MakeRegular(Wrestler w)
        {
            if (ContractOf(w) is not null) return;
            Contracts.Add(new Contract { WrestlerId = w.Id, Kind = ContractKind.Regular, Start = CurrentDate });
        }

        /// <summary>
        /// Lets a wrestler go. A guaranteed deal pays out ninety days and starts a ninety-day
        /// non-compete (doc 13 §3.2); a regular simply stops being booked, which is how a release
        /// happens at the bottom of the business (doc 13 §4). Returns what it cost.
        /// </summary>
        public decimal Release(Wrestler w)
        {
            var contract = ContractOf(w);
            if (contract is null) return 0m;

            decimal payout = 0m;
            if (contract.IsGuaranteed)
            {
                int days = Math.Min(TalentMarket.ReleasePayoutDays, contract.DaysLeft(CurrentDate) ?? 0);
                payout = Math.Round(contract.DailyCost * days);
                Books.Cash -= payout;
                Books.Payroll += payout;
                NonCompetes.Add(new NonCompete { WrestlerId = w.Id, Until = CurrentDate.AddDays(TalentMarket.ReleasePayoutDays), From = PlayerId });
                AddNews($"{Promotion.Name} released {w.RingName}.", aboutYou: true);
            }

            Contracts.Remove(contract);
            Bids.RemoveAll(b => b.WrestlerId == w.Id);
            Log(w, payout > 0 ? $"released, paid out ${payout:N0}" : "no longer a regular");
            return payout;
        }

        internal void Log(Wrestler w, string what)
        {
            ContractLog.Add(new ContractEvent { Date = CurrentDate, WrestlerId = w.Id, What = what });
            if (ContractLog.Count > 40) ContractLog.RemoveAt(0);
        }

        /// <summary>
        /// The day's contract business: guarantees paid, the injured tolled, the expired gone —
        /// to the best rival bid if there is one, to the market if not.
        /// </summary>
        private void SettleContracts()
        {
            decimal payroll = DailyPayroll;
            Books.Cash -= payroll;
            Books.Payroll += payroll;

            foreach (var contract in Contracts.Where(c => c.End is not null).ToList())
            {
                var w = FindWrestler(contract.WrestlerId);

                // Doc 11 §2.1: the contract freezes while they are hurt.
                if (w is not null && w.IsInjured(CurrentDate))
                {
                    contract.End = contract.End!.Value.AddDays(1);
                    contract.DaysTolled++;
                    continue;
                }

                if (CurrentDate < contract.End) continue;

                Contracts.Remove(contract);
                if (w is null) continue;

                if (BestRivalBid(w) is { } bid && Rival(bid.Bidder) is { IsActive: true } rival)
                {
                    rival.Contracts.Add(new Contract
                    {
                        WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = bid.Annual,
                        Start = CurrentDate, End = CurrentDate.AddYears(bid.Years)
                    });
                    VacateTitles(w, $"Left for {rival.Name}");
                    Log(w, $"left for {rival.Name} at ${bid.Annual:N0} a year");
                    AddNews($"{w.RingName} has left {Promotion.Name} for {rival.Name}, signing for ${bid.Annual:N0} a year.", aboutYou: true);
                }
                else
                {
                    Log(w, "contract ran out — on the open market");
                }
                Bids.RemoveAll(b => b.WrestlerId == w.Id);
            }
        }

        /// <summary>Every renewal so far, for the dashboard to report the one that just happened.</summary>
        public TvTerm? LastRenewal => Television?.History.LastOrDefault();

        /// <summary>Every feud the booker has going.</summary>
        public FeudBook FeudBook { get; set; } = new();

        /// <summary>
        /// The promotion's championships. The registry rather than a bare list because
        /// the fixed-attention rule (doc 21 §2.1) can only be enforced across the whole
        /// slate at once.
        /// </summary>
        public TitleRegistry Titles { get; set; } = new();

        /// <summary>Shows scheduled and run, oldest first.</summary>
        public List<ScheduledShow> Shows { get; set; } = new();

        /// <summary>
        /// The promotion's standing commitments — the recurring shows it runs. The
        /// calendar is generated from these; see <see cref="MaterialiseSchedule"/>.
        /// </summary>
        public List<ShowDefinition> ShowDefinitions { get; set; } = new();

        /// <summary>
        /// The promotion's brand structure, and how much of it is still real. Inactive
        /// until the player splits the roster.
        /// See docs/wrestling-reference/22-brand-splits.md.
        /// </summary>
        public BrandSplit Brands { get; set; } = new();

        /// <summary>
        /// When the promotion last ran each gimmick match. Doc 20 §9 asks for stipulation
        /// scarcity "as a promotion-level counter", and this is that counter.
        /// </summary>
        public Engine.StipulationBook Stipulations { get; set; } = new();

        /// <summary>
        /// Standing tag teams. A pair booked together without a team here is two singles
        /// wrestlers on the same side, which is a real and different thing — see
        /// <see cref="TagTeam"/>.
        /// </summary>
        public List<TagTeam> Teams { get; set; } = new();

        /// <summary>
        /// How far ahead the calendar is filled in from the definitions. Kept as a
        /// rolling window rather than generating years at once, and topped up whenever
        /// the clock moves.
        /// </summary>
        public const int ScheduleHorizonDays = 120;

        /// <summary>Real-world clock, for the save list. Not world time.</summary>
        public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The standing team these people are, if they are one. Order-insensitive, and
        /// only matches a team that is exactly this set — a trio is not the tag team of
        /// two of its members.
        /// </summary>
        public TagTeam? TeamFor(IEnumerable<Wrestler> members)
        {
            var set = members.ToList();
            if (set.Count < 2) return null;

            return Teams.FirstOrDefault(t =>
                t.IsActive
                && t.Members.Count == set.Count
                && set.All(t.Contains));
        }

        // ── Calendar queries ─────────────────────────────────────────────────

        public IEnumerable<ScheduledShow> Upcoming =>
            Shows.Where(s => !s.HasRun).OrderBy(s => s.Date).ThenBy(s => s.Name);

        public IEnumerable<ScheduledShow> Completed =>
            Shows.Where(s => s.HasRun).OrderByDescending(s => s.Date);

        /// <summary>Shows on a specific date, in the order they were scheduled.</summary>
        public IEnumerable<ScheduledShow> On(DateOnly date) =>
            Shows.Where(s => s.Date == date);

        /// <summary>
        /// The next show that has not been run. This is what the dashboard counts down to
        /// and what "advance to next show" jumps at.
        /// </summary>
        public ScheduledShow? NextShow => Upcoming.FirstOrDefault();

        /// <summary>
        /// Shows that are due — on or before today and not yet run. A career cannot
        /// advance past one of these; you have to run it or cancel it.
        /// </summary>
        public IEnumerable<ScheduledShow> Due =>
            Shows.Where(s => !s.HasRun && s.Date <= CurrentDate).OrderBy(s => s.Date);

        public bool HasShowDue => Due.Any();

        public int DaysUntil(ScheduledShow show) =>
            show.Date.DayNumber - CurrentDate.DayNumber;

        public int WeeksElapsed => (CurrentDate.DayNumber - StartDate.DayNumber) / 7;

        // ── Roster queries ───────────────────────────────────────────────────

        /// <summary>
        /// By id, through an index. This walked the whole roster comparing strings, and a
        /// promotion forecasting its month asks it once for every contract it holds — in a
        /// world of three thousand that walk was most of what a forecast cost.
        ///
        /// The index is rebuilt when the roster is replaced or changes length, and a miss or a
        /// stale hit falls back to the walk, so it can be slower than it should be but not wrong.
        /// </summary>
        public Wrestler? FindWrestler(string id)
        {
            if (_byId is null || !ReferenceEquals(_indexed, Roster) || _indexedCount != Roster.Count) IndexRoster();
            if (_byId!.TryGetValue(id, out var hit) && hit.Id == id) return hit;

            var found = Roster.FirstOrDefault(w => w.Id == id);
            if (found is not null) IndexRoster();
            return found;
        }

        private Dictionary<string, Wrestler>? _byId;
        private List<Wrestler>? _indexed;
        private int _indexedCount;

        private void IndexRoster()
        {
            _byId = new Dictionary<string, Wrestler>(Roster.Count);
            foreach (var w in Roster) _byId.TryAdd(w.Id, w);
            _indexed = Roster;
            _indexedCount = Roster.Count;
        }

        public IEnumerable<Wrestler> RosterByOverness =>
            Roster.OrderByDescending(w => w.EffectiveOverness);

        // ── Title queries ────────────────────────────────────────────────────

        /// <summary>Belts this person currently holds.</summary>
        public IReadOnlyList<Title> TitlesHeldBy(Wrestler w) => Titles.HeldBy(w);

        // ── Brand queries ────────────────────────────────────────────────────

        /// <summary>The brand running a given date, or null when it is a company-wide show.</summary>
        public Brand? BrandOfShow(ScheduledShow show) =>
            Brands.Active ? Brands.Find(show.BrandId) : null;

        public Brand? BrandOfShow(ShowDefinition definition) =>
            Brands.Active ? Brands.Find(definition.BrandId) : null;

        /// <summary>
        /// The live wrestler instances on a brand, most over first. Brand membership is
        /// stored by id, so this is the only place ids become people.
        /// </summary>
        public IEnumerable<Wrestler> RosterOf(Brand brand) =>
            Roster.Where(w => brand.Contains(w.Id)).OrderByDescending(w => w.EffectiveOverness);

        /// <summary>
        /// Who can work a given show without it counting as a crossover: the brand's own
        /// roster plus anyone unassigned, or everybody when the show has no brand.
        /// </summary>
        public IEnumerable<Wrestler> EligibleFor(ScheduledShow show)
        {
            var brand = BrandOfShow(show);
            if (brand is null) return Roster;

            return Roster.Where(w => brand.Contains(w.Id) || Brands.BrandOf(w.Id) is null);
        }

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Moves the clock forward one day. Refuses to step over a show that is due —
        /// the calendar is a commitment, not a suggestion.
        /// </summary>
        public bool AdvanceOneDay()
        {
            if (HasShowDue) return false;

            CurrentDate = CurrentDate.AddDays(1);

            // The office is open whether or not there is a show tonight, and the guarantees are
            // paid whether or not anybody works.
            ChargeOverhead(1);
            SettleContracts();
            AudienceModel.Decay(Audience);

            // The rest of the world runs on the same clock.
            NonCompetes.RemoveAll(n => n.Until <= CurrentDate);
            Engine.World.Tick(this);

            // The term runs out on the clock, and the network pays next term for what this
            // one drew.
            RenewIfDue();

            // Nothing stays hot. Momentum bleeds toward zero every day, and someone off
            // screen long enough starts to be forgotten — doc 17 §3 and §4.
            foreach (var wrestler in Roster)
                // Measured from the career's start when a wrestler has never worked, so a
                // signing who is never used fades like anybody else off screen.
                HeatEconomy.ApplyDailyDecay(wrestler, CurrentDate, StartDate);

            // The two meters, moving in opposite directions on the same day off. A day of
            // rest pays back fatigue and costs sharpness, which is why there is no amount
            // of time off that is simply good for somebody — see RingCondition.
            //
            // The date goes in because an older body pays fatigue back slower (doc 15 §4),
            // so the same day off is worth less to a veteran than to a twenty-five-year-old.
            foreach (var wrestler in Roster)
                RingCondition.ApplyDayOff(wrestler, CurrentDate);

            // A belt nobody is defending is quietly losing value the whole time — doc 21
            // §4. Being ignored is the fastest killer, so it has to happen on the clock
            // rather than only when someone books a match.
            // Chemistry passed through so the belt reads its champions the same way the
            // crowd does — a drilled team reads closer to its best man, on both sides of
            // the engine rather than only one.
            foreach (var title in Titles.Active)
                TitleEconomy.ApplyDailyDrift(
                    title, CurrentDate,
                    title.CurrentReign is { Champions.Count: > 1 } reign
                        ? TeamFor(reign.Champions)?.Chemistry ?? 0.0
                        : 0.0);

            // A team that stops teaming stops being a team. Same rule as everything else
            // in here — the thing you are not maintaining is quietly getting worse.
            foreach (var team in Teams.Where(t => t.IsActive))
                team.Decay(CurrentDate);

            // And a story nobody is telling stops being a story. Heat only ever went up
            // before this, which made a feud a ratchet: every segment ever booked was still
            // paying off months later, and there was no cost at all to starting five
            // programmes and finishing none. Doc 20 §9 names being left off television as
            // one of the things that kills a feud.
            foreach (var feud in FeudBook.AllIncludingDormant)
                feud.ApplyDailyDecay(CurrentDate);

            // Keep the rolling window full, so the calendar never runs dry ahead of you.
            MaterialiseSchedule();
            return true;
        }

        // ── The books ────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the books with the tier's starting cash and a middling reputation. Called
        /// once when a career begins, and for a career saved before money existed.
        /// </summary>
        public void OpenBooks(DateOnly on)
        {
            var opening = BoxOffice.For(Promotion.Tier).OpeningCash;
            Books = new CareerBooks
            {
                Cash        = opening,
                OpeningCash = opening,
                Reputation  = CareerBooks.OpeningReputation,
                OpenedOn    = on
            };

            if (Promotion.HasTelevision) Television ??= Engine.Television.Open(Promotion.Tier, on);
        }

        /// <summary>
        /// Signs the opening roster from a source — the tier's default, or later a custom
        /// database. Anybody already under contract is left alone.
        /// </summary>
        public void OpenRoster(IOpeningRoster? source = null, IReadOnlyList<Wrestler>? pool = null)
        {
            var opening = (source ?? new TierOpeningRoster()).Open(Promotion, pool ?? Roster, CurrentDate);
            foreach (var c in opening)
                if (Contracts.All(x => x.WrestlerId != c.WrestlerId)) Contracts.Add(c);
        }

        /// <summary>What the promotion costs to keep open for a day, show or no show.</summary>
        public decimal DailyOverhead => BoxOffice.For(Promotion.Tier).DailyOverhead;

        /// <summary>
        /// Renews the television deal if its term has run out. A loop rather than a check, so
        /// a clock that jumps a whole term still renews once per term.
        /// </summary>
        private void RenewIfDue()
        {
            while (Television is { } deal && CurrentDate >= deal.End)
                Engine.Television.Renew(deal, Promotion.Tier);
        }

        private void ChargeOverhead(int days)
        {
            if (days <= 0) return;
            var cost = DailyOverhead * days;
            Books.Cash -= cost;
            Books.Overheads += cost;
        }

        /// <summary>
        /// What the night would take if it ran as booked now. Exact, not an estimate, for the
        /// card — the house is sold on the card as advertised, so what the booking screen shows
        /// is what the night will sell — and a projection for the road, which loses a night to
        /// anybody who gets hurt on it.
        /// </summary>
        public ShowFinances Projection(ScheduledShow show) =>
            Projection(show, Math.Max(0, show.Date.DayNumber - CurrentDate.DayNumber));

        private ShowFinances Projection(ScheduledShow show, int daysAhead) =>
            BoxOffice.Night(show, Promotion, Books.Reputation, FeudBook, Television, AppearanceFee, Interest,
                            w => Response(w, show.Type, daysAhead));

        /// <summary>
        /// Runs a booked show: sells the house, runs the card and the road, banks the takings
        /// and lets the show's quality move reputation. Returns null and does nothing for a
        /// show already run or not runnable.
        ///
        /// The order is the design. The house is sold **before** the card is simulated,
        /// because running a match moves the overness, heat and familiarity the ticket was
        /// bought on. Reputation moves **after**, because it is the show's quality, and that
        /// is only known at the final bell.
        /// </summary>
        public ShowResult? RunShow(ScheduledShow show)
        {
            if (show.HasRun || !show.IsRunnable) return null;

            // Nobody another promotion holds, or who is sitting out a non-compete, works tonight.
            if (UnavailableOnCard(show).Count > 0) return null;

            // Running a date early jumps the clock, and the audience lives through the days it
            // jumps: an act rested until the show is as rested as it will be on the night.
            int ahead = Math.Max(0, show.Date.DayNumber - CurrentDate.DayNumber);
            AudienceModel.Decay(Audience, ahead);

            // Sold on the card as advertised, before anyone has wrestled.
            var house = Projection(show, daysAhead: 0);

            // How new each act's part in tonight is — read now, because running the card moves
            // the familiarity it is read from.
            var novelty = AudienceModel.Novelty(Audience, show.Card, FeudBook, show.Date);

            // The brand context is what charges the night's crossovers to the split. A
            // company-wide date passes a null home brand, which is how the inter-brand
            // supercard stays free — docs/wrestling-reference/22-brand-splits.md §3.4.
            var brands = Brands.Active ? new BrandContext(Brands, BrandOfShow(show)) : null;

            // The card, when there is one. A date can be a run of towns and nothing else,
            // which is the ordinary untelevised week.
            var result = show.Card.Count > 0
                ? new ShowSimulator(FeudBook, titles: Titles, brands: brands,
                                    stipulations: Stipulations, tier: Promotion.Tier,
                                    response: w => Response(w, show.Type))
                    .Simulate(show.ToShow())
                : new ShowResult { BudgetMinutes = show.RuntimeMinutes };

            // What the night did to the audience's patience, and what their patience did to the
            // night: a show like the last few, or one that left its slot half empty, is a worse
            // show. Before reputation and retention read the rating, because both should feel it.
            if (show.Card.Count > 0)
            {
                var audience = AudienceModel.Observe(Audience, show, novelty, result);
                result.OverallRating = Math.Round(
                    result.OverallRating * (1 - audience.SamenessPenalty) * (1 - audience.UnderrunPenalty), 2);
                result.Audience = audience;
            }

            // And the road, when anybody is on it. Anybody on the card works the run a town
            // short: the card is the Monday and the towns are the rest of the week.
            LoopResult? road = null;
            if (show.Loop is { IsBookable: true } loop)
            {
                var onTheCard = show.Card.SelectMany(i => i.Wrestlers).ToHashSet();
                road = new LoopSimulator(tier: Promotion.Tier).Run(loop, show.Date, onTheCard);

                if (road.Workers.Count > 0)
                {
                    result.Loop = road;
                    result.Injuries.AddRange(road.Injuries);
                }
            }

            // The road is settled on the nights actually worked, so somebody sent home hurt
            // is not paid for the towns they missed. Everything else about the night was
            // fixed when the house was sold.
            var finances = house;
            if (road is { Workers.Count: > 0 } && house.Road is { } projected)
                finances.Road = BoxOffice.Worked(projected, road, AppearanceFee);

            if (finances.HadCard)
                Books.Reputation = BoxOffice.ReputationAfter(
                    Books.Reputation, result.OverallRating, show.Type);

            // Running a date early jumps the clock; the days it jumps still cost money. Before the
            // episode is counted, so a show dated after the term's end renews the deal first and
            // counts toward the term it aired in.
            if (CurrentDate < show.Date)
            {
                int skipped = show.Date.DayNumber - CurrentDate.DayNumber;
                ChargeOverhead(skipped);
                var payroll = DailyPayroll * skipped;
                Books.Cash -= payroll;
                Books.Payroll += payroll;
                CurrentDate = show.Date;
                RenewIfDue();
            }

            // Who watched. Tuned in on the card, kept or lost by the show — and that average
            // audience is what the network counts at renewal.
            if (finances.WasTelevised)
            {
                finances.Viewers = (int)Math.Round(
                    finances.TuneIn * Engine.Television.Retention(result.OverallRating));
                Television?.Episodes.Add(new EpisodeRating { Date = show.Date, Viewers = finances.Viewers });
            }

            Books.Cash += finances.Net;
            finances.ReputationAfter = Books.Reputation;
            finances.CashAfter = Books.Cash;
            result.Finances = finances;

            show.Result = result;
            return result;
        }

        /// <summary>
        /// Advances to the next show's date, or by the given cap if that is sooner.
        /// Returns how many days actually passed.
        /// </summary>
        public int AdvanceToNextShow(int maxDays = 60)
        {
            if (HasShowDue) return 0;

            var next = NextShow;
            int target = next != null
                ? Math.Min(DaysUntil(next), maxDays)
                : maxDays;

            int moved = 0;
            while (moved < target && AdvanceOneDay()) moved++;
            return moved;
        }

        /// <summary>
        /// Fills the calendar forward from every active definition, out to the rolling
        /// horizon. Returns the shows it added.
        ///
        /// Only ever adds. An instance the player has renamed, re-venued, booked or run is
        /// never touched, and a date that already has an instance from the same definition
        /// is skipped — so this is safe to call on every clock tick.
        /// </summary>
        public IReadOnlyList<ScheduledShow> MaterialiseSchedule(int? horizonDays = null)
        {
            var added = new List<ScheduledShow>();
            var horizon = CurrentDate.AddDays(horizonDays ?? ScheduleHorizonDays);

            foreach (var definition in ShowDefinitions.Where(d => d.Active))
            {
                // Existing dates for this definition, so a re-run adds nothing.
                var taken = Shows
                    .Where(s => s.DefinitionId == definition.Id)
                    .Select(s => s.Date)
                    .ToHashSet();

                foreach (var date in definition.OccurrencesBetween(CurrentDate, horizon))
                {
                    if (!taken.Add(date)) continue;

                    var show = new ScheduledShow
                    {
                        Name           = definition.Name,
                        Date           = date,
                        Type           = definition.Type,
                        Venue          = definition.Venue,
                        DefinitionId   = definition.Id,
                        BrandId        = definition.BrandId,
                        RuntimeMinutes = definition.RuntimeMinutes ?? Promotion.DefaultRuntimeFor(definition.Type),
                        Attendance     = Promotion.TypicalAttendanceFor(definition.Type)
                    };

                    Shows.Add(show);
                    added.Add(show);
                }
            }

            return added;
        }

        /// <summary>
        /// Stops a definition producing new dates and clears the ones it has already put
        /// on the calendar that nothing has happened to yet, today's included.
        ///
        /// The line is booked-or-run, not future-or-past: a card you have written is work
        /// and survives, an empty placeholder is a setting and follows the definition.
        /// </summary>
        public int RetireDefinition(ShowDefinition definition)
        {
            definition.Active = false;

            var disposable = Shows
                .Where(s => s.DefinitionId == definition.Id
                            && !s.HasRun
                            && !s.IsBooked
                            && s.Date >= CurrentDate)
                .ToList();

            foreach (var show in disposable) Shows.Remove(show);
            return disposable.Count;
        }

        /// <summary>
        /// Applies an edited definition to the calendar: untouched instances from today
        /// forward are discarded and regenerated, so moving Raw to Tuesdays actually moves
        /// it. Booked and run shows are left where they are.
        /// </summary>
        public void ResyncDefinition(ShowDefinition definition)
        {
            var stale = Shows
                .Where(s => s.DefinitionId == definition.Id
                            && !s.HasRun
                            && !s.IsBooked
                            && s.Date >= CurrentDate)
                .ToList();

            foreach (var show in stale) Shows.Remove(show);

            if (definition.Active) MaterialiseSchedule();
        }

        public ScheduledShow Schedule(string name, DateOnly date, ShowType type, string venue = "")
        {
            var show = new ScheduledShow
            {
                Name           = name,
                Date           = date,
                Type           = type,
                Venue          = venue,
                RuntimeMinutes = Promotion.DefaultRuntimeFor(type),
                Attendance     = Promotion.TypicalAttendanceFor(type)
            };

            Shows.Add(show);
            return show;
        }

        public void Cancel(ScheduledShow show)
        {
            if (!show.HasRun) Shows.Remove(show);
        }

        // ── Brands ───────────────────────────────────────────────────────────

        /// <summary>
        /// Divides the promotion. The brands arrive with their rosters already assigned;
        /// this is the moment the structure starts being enforced.
        ///
        /// Integrity resets to the ceiling rather than to 100. A promotion that split,
        /// abandoned it and split again does not get a clean sheet — the audience was
        /// already shown once that the line moves when it is convenient
        /// (docs/wrestling-reference/22-brand-splits.md §4.1).
        /// </summary>
        public void BeginSplit(IEnumerable<Brand> brands)
        {
            Brands.Brands.Clear();
            Brands.Brands.AddRange(brands);

            Brands.Active      = true;
            Brands.StartedOn   = CurrentDate;
            Brands.LastDraftOn = null;
            Brands.Integrity   = Brands.Ceiling;
        }

        /// <summary>
        /// Ends the split and returns every show to the whole roster. Erosion already
        /// booked is kept on the split so a later attempt starts from where this one
        /// finished.
        /// </summary>
        public void EndSplit()
        {
            Brands.End();

            foreach (var definition in ShowDefinitions) definition.BrandId = null;
            foreach (var show in Shows.Where(s => !s.HasRun)) show.BrandId = null;
        }
    }
}
