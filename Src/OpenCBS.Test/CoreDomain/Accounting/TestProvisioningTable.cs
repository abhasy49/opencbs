﻿// LICENSE PLACEHOLDER

using System;
using System.Collections;
using System.Collections.Generic;
using OpenCBS.CoreDomain;
using NUnit.Framework;
using OpenCBS.CoreDomain.Accounting;

namespace OpenCBS.Test.CoreDomain.Accounting
{
	[TestFixture]
	public class TestProvisioningTable
	{
		private ProvisionTable provisionTable;

		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
            provisionTable = ProvisionTable.GetInstance(new User());
            provisionTable.ProvisioningRates = new List<ProvisioningRate>();
            provisionTable.Add(new ProvisioningRate { Number = 1, NbOfDaysMin = 0, NbOfDaysMax = 0, Rate = 2 });
            provisionTable.Add(new ProvisioningRate { Number = 2, NbOfDaysMin = 1, NbOfDaysMax = 30, Rate = 10 });
            provisionTable.Add(new ProvisioningRate { Number = 3, NbOfDaysMin = 31, NbOfDaysMax = 60, Rate = 25 });
		}

		[TestFixtureTearDown]
		public void TestFixtureTearDown()
		{
			provisionTable.ProvisioningRates = new List<ProvisioningRate>();
		}
		
		[Test]
		public void TestProvisioningRateNbOfDaysMin()
		{
			ProvisioningRate pR = new ProvisioningRate();
			pR.NbOfDaysMin = 10;
			Assert.AreEqual(10,pR.NbOfDaysMin);
		}

		[Test]
		public void TestProvisioningRateNbOfDaysMax()
		{
			ProvisioningRate pR = new ProvisioningRate();
			pR.NbOfDaysMax = 20;
			Assert.AreEqual(20,pR.NbOfDaysMax);
		}

		[Test]
		public void TestProvisioningRateRate()
		{
			ProvisioningRate pR = new ProvisioningRate();
			pR.Rate= 10.5;
			Assert.AreEqual(10.5,pR.Rate);
		}

		[Test]
		public void TestGetProvisioningRateByRank()
		{
			Assert.AreEqual(25,provisionTable.GetProvisioningRate(2).Rate);
		}

		[Test]
		public void TestGetProvioningRateWhenNothingFound()
		{
			Assert.IsNull(provisionTable.GetProvisioningRate(-123));
		}

		[Test]
		public void TestGetProvisioningRateByNbOfDays()
		{
			Assert.AreEqual(10,provisionTable.GetProvisiningRateByNbOfDays(21).Rate);
		}

		[Test]
		public void TestGetProvisioningRateByNbOfDaysWhenZero()
		{
			Assert.AreEqual(2,provisionTable.GetProvisiningRateByNbOfDays(0).Rate);
		}

		[Test]
		public void TestGetProvioningRateByNbOfDaysWhenNothingFound()
		{
			Assert.IsNull(provisionTable.GetProvisiningRateByNbOfDays(-123));
		}

	}
}