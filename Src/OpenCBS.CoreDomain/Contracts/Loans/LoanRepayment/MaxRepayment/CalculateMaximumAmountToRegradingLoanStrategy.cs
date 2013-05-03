﻿// LICENSE PLACEHOLDER

using System;
using System.Linq;
using OpenCBS.CoreDomain.Contracts.Loans.Installments;
using OpenCBS.Enums;
using OpenCBS.Shared;
using OpenCBS.Shared.Settings;

namespace OpenCBS.CoreDomain.Contracts.Loans.LoanRepayment.MaxRepayment
{
    /// <summary>
    /// Summary description for MaximumAmountToRegradingLoanStrategy.
    /// </summary>
    [Serializable]
    public class CalculateMaximumAmountToRegradingLoanStrategy
    {
        private readonly Loan _contract;
        private readonly CreditContractOptions _cCo;
        private readonly User _user;
        private readonly ApplicationSettings _generalSettings;
        private readonly NonWorkingDateSingleton _nWds;

        public CalculateMaximumAmountToRegradingLoanStrategy(CreditContractOptions pCCo,Loan pContract, User pUser, 
            ApplicationSettings pGeneralSettings,NonWorkingDateSingleton pNonWorkingDate)
        {
            _user = pUser;
            _generalSettings = pGeneralSettings;
            _nWds = pNonWorkingDate;

            _contract = pContract;
            _cCo = pCCo;
        }

        private OCurrency MaxAmountOfRealSchedule(DateTime payDate)
        {
            Installment installment;

            OCurrency olb = _contract.CalculateActualOlb();
            OCurrency capitalRepayment = 0;
            OCurrency interestPayment = 0;
            OCurrency calculatedInterests;
            OCurrency actualOlb = _contract.CalculateActualOlb();

            bool calculated = false;

            DateTime lasDateOfPayment = _contract.GetLastRepaymentDate();
            int daysInTheYear = _generalSettings.GetDaysInAYear(_contract.StartDate.Year);
            int roundingPoint = _contract.UseCents ? 2 : 0;

            if (_contract.StartDate > lasDateOfPayment)
                lasDateOfPayment = _contract.StartDate.Date;

            for (int i = 0; i < _contract.NbOfInstallments; i++)
            {
                installment = _contract.GetInstallment(i);

                if (installment.IsRepaid)
                {
                    if (lasDateOfPayment < installment.ExpectedDate)
                        lasDateOfPayment = installment.ExpectedDate;
                }

                if (installment.ExpectedDate < payDate)
                {
                    capitalRepayment += installment.CapitalRepayment - installment.PaidCapital;
                    calculated = true;
                }

                if (_contract.StartDate < payDate && installment.Number == 1 && !calculated)
                {
                    capitalRepayment += installment.CapitalRepayment - installment.PaidCapital;
                    calculated = true;
                }

                if (installment.ExpectedDate == payDate && !calculated)
                {
                    capitalRepayment += installment.CapitalRepayment - installment.PaidCapital;
                    calculated = true;
                }

                if (installment.Number > 1 
                    && installment.ExpectedDate != _contract.StartDate 
                    && installment.ExpectedDate > payDate 
                    && _contract.GetInstallment(installment.Number - 2).ExpectedDate < payDate 
                    && !calculated)
                {
                    capitalRepayment += installment.CapitalRepayment - installment.PaidCapital;
                }

                calculated = false;

                if (installment.IsRepaid || installment.InterestHasToPay == 0) continue;

                if (installment.ExpectedDate <= payDate)
                {
                    calculatedInterests = 0;

                    if (installment.PaidInterests > 0 
                        && installment.InterestsRepayment > installment.PaidInterests)
                    {
                        calculatedInterests = installment.PaidInterests;
                    }

                    if (installment.PaidCapital == 0 
                        && installment.PaidInterests > 0 
                        && installment.PaidInterests != installment.InterestsRepayment)
                    {
                        DateTime dateOfInstallment = installment.Number == 1
                                   ? _contract.StartDate
                                   : _contract.GetInstallment(installment.Number - 2).ExpectedDate;
                        int d = (lasDateOfPayment - dateOfInstallment).Days;
                        OCurrency olbBeforePayment =
                            _contract.Events.GetRepaymentEvents().Where(
                                repaymentEvent => !repaymentEvent.Deleted && repaymentEvent.Date <= dateOfInstallment).Aggregate(
                                    _contract.Amount, (current, repaymentEvent) => current - repaymentEvent.Principal);

                        calculatedInterests =
                            (olbBeforePayment*Convert.ToDecimal(_contract.InterestRate)/daysInTheYear*d).Value;
                        calculatedInterests = Math.Round(calculatedInterests.Value, roundingPoint,
                                                         MidpointRounding.AwayFromZero);

                        if (installment.PaidInterests < calculatedInterests && actualOlb != olbBeforePayment)
                        {
                            calculatedInterests = installment.PaidInterests;
                        }
                    }
                    DateTime expectedDate = installment.ExpectedDate;
                    //in case very late repayment
                    if (installment.Number == _contract.InstallmentList.Count
                        && installment.ExpectedDate < payDate
                        && installment.PaidCapital == 0)
                    {
                        expectedDate = payDate;
                    }

                    int days = (expectedDate - lasDateOfPayment).Days;
                    interestPayment += Math.Round((olb * _contract.InterestRate / daysInTheYear * days).Value + calculatedInterests.Value,
                                                    roundingPoint, MidpointRounding.AwayFromZero) - installment.PaidInterests;
                    lasDateOfPayment = installment.ExpectedDate;
                }

                if (installment.Number > 1 
                    && installment.ExpectedDate > payDate 
                    && installment.ExpectedDate > payDate 
                    && _contract.GetInstallment(installment.Number - 2).ExpectedDate < payDate)
                {
                    OCurrency paidInterests = installment.PaidInterests;

                    int daySpan = (payDate - lasDateOfPayment).Days < 0 ? 0 : (payDate - lasDateOfPayment).Days;
                    
                    installment.InterestsRepayment = olb * _contract.InterestRate * daySpan / daysInTheYear + paidInterests;

                    installment.InterestsRepayment =
                        Math.Round(installment.InterestsRepayment.Value, roundingPoint,
                                   MidpointRounding.AwayFromZero);
                    interestPayment += installment.InterestsRepayment - paidInterests;

                    lasDateOfPayment = installment.ExpectedDate;
                }

                if (installment.Number == 1 && installment.ExpectedDate > payDate)
                {
                    int daySpan = (payDate - _contract.StartDate).Days < 0
                                      ? 0
                                      : (payDate - _contract.StartDate).Days;
                    OCurrency interest = olb * _contract.InterestRate * daySpan / daysInTheYear;
                    interestPayment += Math.Round(interest.Value, roundingPoint, MidpointRounding.AwayFromZero) - installment.PaidInterests; 
                }
            }

            return interestPayment + capitalRepayment < 0 ? 0 : interestPayment + capitalRepayment;
        }

        public OCurrency CalculateMaximumAmountToRegradingLoan(DateTime pDate)
        {
            if (_cCo.LoansType == OLoanTypes.DecliningFixedPrincipalWithRealInterest)
                return MaxAmountOfRealSchedule(pDate);

            OCurrency amount = 0;
            //capital
            amount += _contract.CalculateActualOlb(pDate);

            //interest
            if (_cCo.CancelInterests)
                amount += _cCo.ManualInterestsAmount;
            else 
                amount += _contract.CalculateRemainingInterests(pDate);

            //fees
            if (_cCo.CancelFees)
            {
                amount += _cCo.ManualFeesAmount;
                amount += _cCo.ManualCommissionAmount;
            }
            else
                amount += _CalculateLateAndAnticipatedFees(pDate);

            int decimalPoint = _contract.UseCents ? _generalSettings.InterestRateDecimalPlaces : 0;
            return Math.Round(amount.Value, decimalPoint, MidpointRounding.AwayFromZero);
        }

        private OCurrency _CalculateLateAndAnticipatedFees(DateTime pDate)
        {
            OCurrency fees = 0;
            Loan contract = _contract.Copy();
            new Repayment.RepayLateInstallments.CalculateInstallments(_cCo, contract, _user, _generalSettings,_nWds).CalculateNewInstallmentsWithLateFees(pDate);
            for (int i = 0; i < contract.NbOfInstallments; i++)
            {
                Installment installment = contract.GetInstallment(i);
                if (!installment.IsRepaid && installment.ExpectedDate <= pDate)
                {
                    fees += installment.FeesUnpaid;
                    installment.PaidCapital = installment.CapitalRepayment;
                    installment.PaidInterests = installment.InterestsRepayment;
                }
            }
            return fees;
        }
    }
}