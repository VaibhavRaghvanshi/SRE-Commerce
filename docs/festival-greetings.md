# Storefront festival greetings

The public website displays the same greeting to guests and signed-in customers. Diwali, Holi and Christmas display from two days before through one day after the festival, inclusive. Other listed greetings use their calendar date. The banner follows the storefront's English or Hindi language preference.

The browser selects a country using a recognized time zone, then an explicit region in the browser locale. Dates follow the browser's local calendar day. No location permission, paid service, API call, account data or database is needed. The initial country calendars cover India, Singapore, Canada and the United States. Christmas and New Year greetings are shared worldwide.

Movable dates such as Holi and Diwali are recorded for 2026 and 2027 in [the Angular calendar](../SRECommerce.UI/src/app/shared/festival-banner/festival-calendar.ts). Add a new year when the official dates are published. The calendar does not repeat a previous year's date. Fixed annual dates and the weekday rules for Canadian and US Thanksgiving continue each year.

- India. [2026 Government of India calendar](https://www.iirs.gov.in/holidaycalender), [2027 India national portal for Holi](https://www.india.gov.in/calendar?date=2027-03-01), [2027 High Commission calendar](https://www.hcilondon.gov.in/page/list-of-the-holidays/)
- Singapore. [2026 gazetted dates](https://www.mom.gov.sg/newsroom/press-releases/2025/0616-public-holidays-for-2026), [2027 gazetted dates](https://www.mom.gov.sg/newsroom/press-releases/2026/0618-public-holidays-for-2027)
- North America. [Canadian holiday calendar](https://www.canada.ca/en/library-archives/services/public/visit/holiday-closures.html), [US holiday dates](https://uscode.house.gov/us/usc/119-136/t5/s6103/)
