import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { OrdersStore } from '../../orders.store';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss',
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  protected readonly ordersStore = inject(OrdersStore);

  async ngOnInit(): Promise<void> {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      await this.ordersStore.loadOrderDetail(orderId);
    }
  }

  protected formatAmount(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }

  // Client-side approximation only (Story 5.1, AC #3) — avoids showing a "Demander un retour"
  // button that would always fail, but the backend (using Order.LastModified, not this DTO's
  // `date`/Created) is the actual source of truth for the 14-day window; a false positive here
  // just means the customer sees the backend's own 422 message instead of the button being
  // hidden a little early.
  protected isReturnEligible(order: { status: string; date: string }): boolean {
    const fourteenDaysMs = 14 * 24 * 60 * 60 * 1000;
    return order.status === 'Livrée' && Date.now() - new Date(order.date).getTime() <= fourteenDaysMs;
  }
}
