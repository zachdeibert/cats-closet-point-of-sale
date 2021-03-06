﻿using System;
using System.Linq;
using CatsCloset.Emails;
using CatsCloset.Model;
using CatsCloset.Model.Requests;
using CatsCloset.Model.Responses;

namespace CatsCloset.Apis {
	public class Purchase : AbstractApi<PurchaseRequest, StatusResponse> {
		protected override StatusResponse Handle(PurchaseRequest req) {
			User user = RequireAuthentication();
			AccessRequire(user.StoreAccess);
			lock ( Context ) {
				double taxFactor = 1 +
					double.Parse((
						Context.Options
						.FirstOrDefault(
							o => o.Key == "Tax")
						?? new Option() { Value = "0" })
						.Value);
				int cost = (int) (req.purchases
				.Select(
			             i => Context.Products
					.First(
			             p => p.Id == i)
					.Price)
				.Sum() * taxFactor);
				Customer customer = Context.Customers
				.First(
                   c => c.Barcode ==
						req.barcode);
				if (customer.Pin.Trim('-').Length == 0) {
					customer.Pin = req.pin;
					customer.SendSetPinEmail();
				}
				if (customer.Pin == req.pin && customer.Balance >= cost) {
					customer.Balance -= cost;
					History history = new History();
					history.BalanceChange = -cost;
					history.Time = DateTime.Now;
					history.User = user;
					history.Customer = customer;
					Context.History.Add(history);
					Context.HistoryPurchases.AddRange(
						req.purchases
						.Select(
							i =>
							Context.Products
							.First(
								p => p.Id == i))
						.GroupBy(
							p => p.Id)
						.Select(g => new HistoryPurchase {
							Amount = g.Count(),
							History = history,
							Product = g.First()
						}));
					Context.SaveChanges();
					customer.SendPurchaseEmail(history);
					return new StatusResponse(true);
				} else {
					return new StatusResponse(false);
				}
			}
		}

		public Purchase() : base("/purchase") {
		}
	}
}

