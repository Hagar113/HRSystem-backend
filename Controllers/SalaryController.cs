﻿using GraduationProject.DTOs;
using GraduationProject.Helpers;
using GraduationProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GraduationProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalaryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public SalaryController(ApplicationDbContext context)
        {
            _context = context;
        }
        #region get all emp
        [HttpGet("GetALLEmpsSalaries")]
        public IActionResult GetALLEmpsSalaries()
        {
            var emps = _context.Employees.ToList();
            List<SalaryResponseDto> salaryResponseDtos = new List<SalaryResponseDto>();

            SalaryCalculate salary = new SalaryCalculate(_context);

            foreach (var emp in emps)
            {
                salaryResponseDtos.Add(salary.CalcSalary(emp.Id));
            }
            return Ok(salaryResponseDtos);
        }
        #endregion
        #region get


        [HttpGet("{id}")]
        public IActionResult GetEmployeSalary(int id)
        {

            var Emp = _context.Employees
                                .Include(h => h.dept)
                                .Include(h => h.salary)
                                .FirstOrDefault(h => h.Id == id);

            var settings = _context.generalSettings.OrderByDescending(h => h.Id).FirstOrDefault();

            var firstWeekDay = APIsHelper.GetNumberOfWeekdaysInMonth(settings != null && settings.SelectedFirstWeekendDay != null ? settings.SelectedFirstWeekendDay : "");
            var secondWeekDay = APIsHelper.GetNumberOfWeekdaysInMonth(settings != null && settings.SelectedSecondWeekendDay != null ? settings.SelectedSecondWeekendDay : "");

            int firstWeekDaysCount = (int)(firstWeekDay != null ? firstWeekDay : 0);
            int secondWeekDaysCount = (int)(secondWeekDay != null ? secondWeekDay : 0);

            var holidays = _context.Holidays.Where(h => h.Date.Month == DateTime.Now.Month).ToList();

            int HolidaysCount = holidays != null ? holidays.Count() : 0;

            int daysInCurrentMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);

            int totalOfficialDaysInThisMonth = daysInCurrentMonth - (firstWeekDaysCount + secondWeekDaysCount + HolidaysCount);
            //var dayyyyss = (daysInCurrentMonth - (firstWeekDaysCount + secondWeekDaysCount));

            double DayPrice = Emp.salary.NetSalary / 22;
            //double DayPrice = Emp.salary.NetSalary / dayyyyss;

            DateTime leaveTime = DateTime.Parse(Emp.LeaveTime);
            DateTime attendanceTime = DateTime.Parse(Emp.AttendanceTime);
            double timeDifferenceInHours = (leaveTime - attendanceTime).TotalHours;


            double HourPrice = DayPrice / timeDifferenceInHours;


            var attendances = _context.EmployeeAttendances.Where(z => z.EmployeeId == id && z.Attendence.Month == DateTime.Now.Month).ToList();



            int attendedDaysCount = attendances != null ? attendances.Where(h => h.Departure != null).Count() : 0;

            int absenceDays = totalOfficialDaysInThisMonth - attendedDaysCount;

            double extraHours = 0;
            double lossHours = 0;

            foreach (var attendance in attendances)
            {
                if (attendance.Departure != null)
                {
                    TimeSpan timeDifference = new TimeSpan();
                    double attendanceTimeEmp = 0.0;

                    timeDifference = attendanceTime.TimeOfDay - attendance.Attendence.TimeOfDay;

                    attendanceTimeEmp = timeDifference.TotalHours;

                    TimeSpan timeDifference2 = new TimeSpan();
                    double departureTimeEmp = 0.0;

                    timeDifference2 = attendance.Departure.Value.TimeOfDay - leaveTime.TimeOfDay;

                    departureTimeEmp = timeDifference2.TotalHours;


                    var resetHours = attendanceTimeEmp + departureTimeEmp;

                    if (attendanceTimeEmp > 0)
                    {
                        extraHours += attendanceTimeEmp;
                    }
                    else
                    {
                        lossHours += attendanceTimeEmp;
                    }

                    if (departureTimeEmp > 0)
                    {
                        extraHours += departureTimeEmp;
                    }
                    else
                    {
                        lossHours += departureTimeEmp;
                    }
                }

            }

            double extraHoursAdjustment = settings.Addition ?? 0;
            double discountHoursAdjustment = settings.Deduction ?? 0;

            if (settings.Method == "hour")
            {
                extraHours *= extraHoursAdjustment;
                lossHours *= discountHoursAdjustment;
            }

            int absenceDayss = totalOfficialDaysInThisMonth - attendances?.Count() ?? 0;

            SalaryResponseDto responseDto = new SalaryResponseDto();
            responseDto.empName = Emp.Name;
            responseDto.NetSalary = Emp.salary.NetSalary;
            responseDto.deptName = Emp.dept.Name;
            responseDto.attendanceDays = attendedDaysCount;
            responseDto.absenceDays = absenceDayss;
            responseDto.exrtaHours = extraHours;
            responseDto.discountHours = lossHours;
            responseDto.extraSalary = (double)(settings.Method == "hour" ? (extraHours * HourPrice) : (extraHours * settings.Addition));
            responseDto.discountSalary = (double)(settings.Method == "hour" ? (lossHours * HourPrice) : (lossHours * settings.Deduction));
            responseDto.HourlyRate = HourPrice;

            double totalSalaey = responseDto.NetSalary + responseDto.extraSalary + responseDto.discountSalary;

            responseDto.totalSalary = totalSalaey - (DayPrice * (22- attendedDaysCount));
            return Ok(responseDto);

        }

        private int CountWeekendsInMonth(int year, int month, List<string> selectedWeekendDays)
        {
            int weekendsCount = 0;
            int daysInMonth = DateTime.DaysInMonth(year, month);

            for (int i = 1; i <= daysInMonth; i++)
            {
                var currentDate = new DateTime(year, month, i);
                if (selectedWeekendDays.Contains(currentDate.DayOfWeek.ToString()))
                {
                    weekendsCount++;
                }
            }

            return weekendsCount;
        }

        private int CountWeekdaysInMonth(int year, int month, string? dayOfWeek)
        {
            int count = 0;
            int daysInMonth = DateTime.DaysInMonth(year, month);

            for (int i = 1; i <= daysInMonth; i++)
            {
                var currentDate = new DateTime(year, month, i);
                if (currentDate.DayOfWeek.ToString() == dayOfWeek)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion
        #region search
        [HttpGet("SearchEmployees")]
        public IActionResult GetSalaryReport(int month, int year)
        {
            var employees = _context.Employees
                .Include(e => e.dept)
                .Include(e => e.salary)
                .ToList();

            var settings = _context.generalSettings.OrderByDescending(s => s.Id).FirstOrDefault();

            if (settings == null)
            {
                return BadRequest("General settings not found.");
            }

            var generalSettingDTO = new GeneralSettingDTO
            {
                Deduction = settings.Deduction,
                Addition = settings.Addition,
                SelectedFirstWeekendDay = settings.SelectedFirstWeekendDay,
                SelectedSecondWeekendDay = settings.SelectedSecondWeekendDay,
            };

            var filteredSalaries = new List<SalaryResponseDto>();

            var employeesForMonthYear = employees.Where(e => _context.EmployeeAttendances.Any(a => a.EmployeeId == e.Id && a.Attendence.Month == month && a.Attendence.Year == year)).ToList();

            if (employeesForMonthYear.Count == 0)//IMP
            {
                return NotFound("No employees found for the selected month and year.");
            }

            foreach (var employee in employees)
            {


                var firstWeekDay = settings != null ? settings.SelectedFirstWeekendDay : null;
                var secondWeekDay = settings != null ? settings.SelectedSecondWeekendDay : null;

                int firstWeekDaysCount = !string.IsNullOrEmpty(firstWeekDay) ? CountWeekdaysInMonth(year, month, firstWeekDay) : 0;
                int secondWeekDaysCount = !string.IsNullOrEmpty(secondWeekDay) ? CountWeekdaysInMonth(year, month, secondWeekDay) : 0;

                //double DayPrice = employee.salary.NetSalary / DateTime.DaysInMonth(year, month);
                double DayPrice = employee.salary.NetSalary / 22;

                DateTime leaveTime = DateTime.Parse(employee.LeaveTime);
                DateTime attendanceTime = DateTime.Parse(employee.AttendanceTime);


                double timeDifferenceInHours = (leaveTime - attendanceTime).TotalHours;

                double HourPrice = timeDifferenceInHours > 0 ? DayPrice / timeDifferenceInHours : 0;

                var holidays = _context.Holidays.Where(h => h.Date.Month == month && h.Date.Year == year).ToList();
                int HolidaysCount = holidays?.Count() ?? 0;

                int daysInCurrentMonth = DateTime.DaysInMonth(year, month);

                //int daysInCurrentMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);

                var selectedWeekendDays = new List<string> { generalSettingDTO.SelectedFirstWeekendDay, generalSettingDTO.SelectedSecondWeekendDay };
                int weekendsInMonth = CountWeekendsInMonth(year, month, selectedWeekendDays);
                //int weekendsInMonth = CountWeekendsInMonth(DateTime.Now.Year, DateTime.Now.Month, selectedWeekendDays);

                var selectedVacationDaysAsStrings = generalSettingDTO.SelectedVacationDays?.Select(day => ((DayOfWeek)day).ToString()).ToList() ?? new List<string>();//IMP

                int totalOfficialDaysInThisMonth = daysInCurrentMonth - (firstWeekDaysCount + secondWeekDaysCount + HolidaysCount);

                var attendances = _context.EmployeeAttendances
                    .Where(z => z.EmployeeId == employee.Id && z.Attendence.Month == month && z.Attendence.Year == year)
                    .ToList();

                int absenceDayss = totalOfficialDaysInThisMonth - attendances?.Where(e => e.Departure != null).Count() ?? 0;

                var holidaysAndWeekends = holidays.Select(h => h.Date.DayOfWeek.ToString())
                    .Concat(selectedWeekendDays)
                    .Distinct();

                double extraHours = 0;
                double lossHours = 0;


                foreach (var attendance in attendances)
                {
                    if (attendance.Departure != null)
                    {
                        TimeSpan timeDifference = new TimeSpan();
                        double attendanceTimeEmp = 0.0;

                        timeDifference = attendanceTime.TimeOfDay - attendance.Attendence.TimeOfDay;

                        attendanceTimeEmp = timeDifference.TotalHours;

                        TimeSpan timeDifference2 = new TimeSpan();
                        double departureTimeEmp = 0.0;

                        timeDifference2 = attendance.Departure.Value.TimeOfDay - leaveTime.TimeOfDay;

                        departureTimeEmp = timeDifference2.TotalHours;


                        var resetHours = attendanceTimeEmp + departureTimeEmp;

                        if (attendanceTimeEmp > 0)
                        {
                            extraHours += attendanceTimeEmp;
                        }
                        else
                        {
                            lossHours += attendanceTimeEmp;
                        }

                        if (departureTimeEmp > 0)
                        {
                            extraHours += departureTimeEmp;
                        }
                        else
                        {
                            lossHours += departureTimeEmp;
                        }
                    }

                }



                double extraHoursAdjustment = settings.Addition ?? 0;
                double discountHoursAdjustment = settings.Deduction ?? 0;

                if (settings.Method == "hour")
                {
                    extraHours *= extraHoursAdjustment;
                    lossHours *= discountHoursAdjustment;
                }

                var salary = new SalaryResponseDto
                {
                    empName = employee.Name,
                    NetSalary = employee.salary.NetSalary,
                    deptName = employee.dept.Name,
                    attendanceDays = attendances?.Where(e => e.Departure != null).Count() ?? 0,
                    absenceDays = absenceDayss,
                    exrtaHours = extraHours,
                    discountHours = lossHours,
                    extraSalary = (double)(settings.Method == "hour" ? (extraHours * HourPrice) : (extraHours * settings.Addition)),
                    discountSalary = (double)(settings.Method == "hour" ? (lossHours * HourPrice) : (lossHours * settings.Deduction)),
                    HourlyRate = HourPrice,
                    DailyRate = DayPrice,
                    WeekendDays = weekendsInMonth,
                    Month = month,
                    Year = year,
                };
                double totalSalarry = employee.salary.NetSalary + salary.extraSalary + salary.discountSalary;

                //salary.totalSalary = totalSalarry - (DayPrice * (totalOfficialDaysInThisMonth - attendances.Count()));

                salary.totalSalary = totalSalarry - (DayPrice * (22- attendances.Where(e => e.Departure != null).Count()));

                filteredSalaries.Add(salary);
            }

            return Ok(filteredSalaries);
        }


        // Helper method to calculate delay hours
        private double CalculateDelayHours(DateTime arrivalTime, DateTime scheduledArrivalTime)
        {
            if (arrivalTime > scheduledArrivalTime)
            {
                TimeSpan delay = arrivalTime - scheduledArrivalTime;
                return delay.TotalHours;
            }
            return 0;
        }

        // Helper method to calculate early arrival hours
        private double CalculateEarlyArrivalHours(DateTime arrivalTime, DateTime scheduledArrivalTime)
        {
            if (arrivalTime < scheduledArrivalTime)
            {
                TimeSpan earlyArrival = scheduledArrivalTime - arrivalTime;
                return earlyArrival.TotalHours;
            }
            return 0;
        }
        #endregion 
        #region details

        [HttpGet("GetEmployeeAttendanceDetails")]
        public IActionResult GetEmployeeAttendanceDetails(int employeeId, int month, int year)
        {
            var employee = _context.Employees
                .Include(e => e.dept)
                .Include(e => e.salary)
                .FirstOrDefault(e => e.Id == employeeId );

            if (employee == null)
            {
                return BadRequest("Employee not found or resigned.");
            }

            var settings = _context.generalSettings.OrderByDescending(s => s.Id).FirstOrDefault();

            if (settings == null)
            {
                return BadRequest("General settings not found.");
            }

            var generalSettingDTO = new GeneralSettingDTO
            {
                Deduction = settings.Deduction,
                Addition= settings.Addition,
                SelectedFirstWeekendDay = settings.SelectedFirstWeekendDay,
                SelectedSecondWeekendDay = settings.SelectedSecondWeekendDay,
                Method = settings.Method,    /////////////////////////////////////////// New
            };

            var firstWeekDay = settings != null ? settings.SelectedFirstWeekendDay : null;
            var secondWeekDay = settings != null ? settings.SelectedSecondWeekendDay : null;

            int firstWeekDaysCount = !string.IsNullOrEmpty(firstWeekDay) ? CountWeekdaysInMonth(year, month, firstWeekDay) : 0;
            int secondWeekDaysCount = !string.IsNullOrEmpty(secondWeekDay) ? CountWeekdaysInMonth(year, month, secondWeekDay) : 0;

            double DayPrice = employee.salary.NetSalary / DateTime.DaysInMonth(year, month);

            var attendances = _context.EmployeeAttendances
                .Where(z => z.EmployeeId == employeeId && z.Attendence.Month == month && z.Attendence.Year == year)
                .OrderBy(a => a.Attendence)
                .ToList();

            var attendanceDetails = new List<AttendanceResponse>();

            DateTime leaveTime = DateTime.Parse(employee.LeaveTime);
            DateTime attendanceTime = DateTime.Parse(employee.AttendanceTime);



            foreach (var attendance in attendances)
            {
                if (attendance.Departure != null)
                {
                    TimeSpan timeDifference = new TimeSpan();
                    double attendanceTimeEmp = 0.0;

                    timeDifference = attendanceTime.TimeOfDay - attendance.Attendence.TimeOfDay;

                    attendanceTimeEmp = timeDifference.TotalHours;

                    TimeSpan timeDifference2 = new TimeSpan();
                    double departureTimeEmp = 0.0;

                    timeDifference2 = attendance.Departure.Value.TimeOfDay - leaveTime.TimeOfDay;

                    departureTimeEmp = timeDifference2.TotalHours;


                    var resetHours = attendanceTimeEmp + departureTimeEmp;

                    double extraHours = 0;
                    double lossHours = 0;

                    if (attendanceTimeEmp > 0)
                    {
                        extraHours += attendanceTimeEmp;
                    }
                    else
                    {
                        lossHours += attendanceTimeEmp;
                    }

                    if (departureTimeEmp > 0)
                    {
                        extraHours += departureTimeEmp;
                    }
                    else
                    {
                        lossHours += departureTimeEmp;
                    }

                    var attendanceDetail = new AttendanceResponse
                    {
                        id = attendance.Id,
                        name = employee.Name,
                        department = employee?.dept?.Name,
                        attend = attendance.Attendence.ToString("HH:mm"),
                        leave = attendance.Departure.Value.ToString("HH:mm"),
                        date = attendance.Attendence.Date.ToString("yyyy-MM-dd"),
                        OriginalAttend = employee.AttendanceTime,
                        OriginalLeave = employee.LeaveTime,

                        ExtraHours = extraHours,
                        EarlyDepartureHours = lossHours,
                    };

                    attendanceDetails.Add(attendanceDetail);

                }
            }

            return Ok(attendanceDetails);
        }
        #endregion
    }

}


