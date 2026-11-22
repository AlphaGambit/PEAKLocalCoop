using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakLocalCoop;

internal sealed class LocalParty
{
    public sealed class Seat
    {
        public int Number { get; }
        public Guid Id { get; } = Guid.NewGuid();
        public Gamepad Pad { get; private set; }
        private string serial;

        public Seat(int number, Gamepad pad)
        {
            Number = number;
            Attach(pad);
        }

        public void Attach(Gamepad pad)
        {
            Pad = pad;
            serial = pad?.description.serial;
        }

        public bool Matches(Gamepad pad) => ReferenceEquals(Pad, pad) ||
            (!string.IsNullOrWhiteSpace(serial) && serial == pad.description.serial);
    }

    private readonly List<Seat> seats = new List<Seat>(4);
    private float lastJoinTime = float.NegativeInfinity;
    public IReadOnlyList<Seat> Seats => seats;

    public void Reset()
    {
        seats.Clear();
        seats.Add(new Seat(1, null));
        lastJoinTime = float.NegativeInfinity;
    }

    public Seat Add(Gamepad pad)
    {
        if (!pad.added || seats.Count == 4 || seats.Any(seat => seat.Matches(pad)) ||
            Time.unscaledTime - lastJoinTime < 0.4f)
            return null;
        var seat = new Seat(seats.Count + 1, pad);
        seats.Add(seat);
        lastJoinTime = Time.unscaledTime;
        return seat;
    }

    public void RemoveLast(Seat seat)
    {
        if (seats.Count > 1 && seats[seats.Count - 1] == seat) seats.RemoveAt(seats.Count - 1);
    }

    public bool AttachReplacement(Seat seat, Gamepad pad)
    {
        if (seat.Number == 1 || (seat.Pad != null && seat.Pad.added)
            || seats.Any(other => other != seat && other.Matches(pad)))
            return false;
        seat.Attach(pad);
        return true;
    }
}
