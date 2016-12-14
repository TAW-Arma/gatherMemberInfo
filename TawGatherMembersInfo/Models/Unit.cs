﻿using Neitri;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TawGatherMembersInfo.Models
{
	public class Unit
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public virtual long UnitId { get; set; }

		[Index(IsUnique = true)]
		public virtual int TawId { get; set; }

		/// <summary>
		/// Possible values: Division, Batallion, Platoon, Squad, Fire Team, and more
		/// </summary>
		[StringLength(500)]
		public virtual string Type { get; set; } = "";

		[StringLength(500)]
		public virtual string Name { get; set; } = "noname";

		public virtual Unit ParentUnit { get; set; }
		public virtual ICollection<Unit> ChildUnits { get; set; }

		public virtual ICollection<PersonUnit> People { get; set; }

		public virtual ICollection<Event> Events { get; set; }

		public Dictionary<Person, string> PersonToPositionNameShort => People.ToDictionary(p => p.Person, p => p.PositionNameShort);

		[NotMapped]
		public Person HighestRankingPerson
		{
			get
			{
				Person highestPerson = null;
				int highestPriority = int.MinValue;

				foreach (var personUnit in People)
				{
					if (personUnit.Removed < DateTime.UtcNow) continue;

					var positionNameShort = personUnit.PositionNameShort;
					var person = personUnit.Person;

					var positionPriority = Person.positionNameShortTeamSpeakNamePriorityOrder.IndexOf(positionNameShort);
					if (positionPriority > highestPriority)
					{
						highestPriority = positionPriority;
						highestPerson = person;
					}
				}

				return highestPerson;
			}
		}

		/// <summary>
		/// Returns the prefix you use fot TeamSpeak name
		/// AM1 1st Battalion North American == AM 1
		/// AM2 2nd Battalion European == AM 2
		/// SOCOP = SOCOP
		/// </summary>
		[NotMapped]
		public string TeamSpeakNamePrefix
		{
			get
			{
				// special use cases
				if (Type.ToLower() == "division" && Name.ToLower().Contains("arma ")) return "AM";

				if (Name.IsNullOrWhiteSpace()) return null;
				if (Name.Contains(" ") == false) return null;

				var prefix = Name.TakeStringBefore(" "); // AM1 1st Battalion North American || AM2 2nd Battalion European
														 // we take only AM1 or AM2
				if (prefix.IsNullOrWhiteSpace()) return null;

				// special use case
				if (prefix == "TAW") return null;

				// then take the last number out of it
				int lastCharAsInt;
				var isLastCharNumber = int.TryParse(prefix.Last().ToString(), out lastCharAsInt);
				if (isLastCharNumber)
				{
					// first part of battalion name is TS prefix
					prefix = prefix.RemoveFromEnd(1) + " " + lastCharAsInt;
				}

				// only take prefix if its uppercase and looks valid
				if (prefixValidRegexp.IsMatch(prefix)) return prefix;
				return null;
			}
		}

		static Regex prefixValidRegexp = new Regex("^[0-9 A-Z]+$", RegexOptions.Compiled);

		public static string GetUnitRoasterPage(int unitTawId)
		{
			if (unitTawId < 1) throw new IndexOutOfRangeException("unit id must be 1 or more");
			return @"http://taw.net/unit/" + unitTawId + "/roster.aspx";
		}

		private void FillWithAllActivePeopleNames(HashSet<string> people)
		{
			var names = this.People.Where(p => !p.Person.Status.StartsWith("discharged")).Select(p => p.Person.Name);
			people.UnionWith(names);
			foreach (var unit in ChildUnits) unit.FillWithAllActivePeopleNames(people);
		}

		public HashSet<string> GetPeopleInUnit()
		{
			var hs = new HashSet<string>();
			FillWithAllActivePeopleNames(hs);
			return hs;
		}

		private void FillWithAllActivePeople(HashSet<Person> resultPeople)
		{
			var utcNow = DateTime.UtcNow;
			var people = this.People.Where(p => p.Removed > utcNow).Select(p => p.Person);
			resultPeople.UnionWith(people);
			foreach (var unit in ChildUnits) unit.FillWithAllActivePeople(resultPeople);
		}

		public HashSet<Person> GetAllActivePeople()
		{
			var hs = new HashSet<Person>();
			FillWithAllActivePeople(hs);
			return hs;
		}

		public string PrettyTreePrint(int depth = 0)
		{
			var sb = new StringBuilder();
			sb.AppendLine();
			for (int i = 1; i < depth; i++) sb.Append("|");
			sb.Append("|unit=" + Type + "=" + Name + " tawId:" + UnitId);
			foreach (var c in People)
			{
				sb.AppendLine();
				for (int i = 0; i < depth; i++) sb.Append("|");
				sb.Append("|");
				sb.Append("person=" + c.Person + "=" + c.PositionNameShort);
			}
			foreach (var c in ChildUnits)
			{
				sb.Append(c.PrettyTreePrint(depth + 1));
			}
			return sb.ToString();
		}

		public override string ToString()
		{
			return Type + " - " + Name;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as Unit);
		}

		public bool Equals(Unit other)
		{
			if (other == null) return false;
			return UnitId == other.UnitId;
		}

		public override int GetHashCode()
		{
			return UnitId.GetHashCode();
		}
	}
}