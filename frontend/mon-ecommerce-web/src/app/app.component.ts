import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ToastComponent } from './core/components/toast/toast.component';
import { HeaderComponent } from './core/components/header/header.component';
import { CartDrawerComponent } from './core/components/cart-drawer/cart-drawer.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastComponent, HeaderComponent, CartDrawerComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {}
